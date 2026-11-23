using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.Common.Exceptions;
using ProjectManager.API.Data;
using ProjectManager.API.DTOs.Git;
using ProjectManager.API.Hubs;
using ProjectManager.API.Model;
using ProjectManager.API.Services.ActivityService;
using ProjectManager.API.Services.CurrentUserService;
using ProjectManager.API.Services.EncryptionService;
using ProjectManager.API.Services.GitWebhookService;
using ProjectManager.API.Services.GitWebhookService.Payloads;
using ProjectManager.API.Services.RateLimit;
using System.Diagnostics;

namespace ProjectManager.API.Services.GitBackfillService
{
    /// <summary>
    /// A hiányosan érkezett pushok pótlása.
    ///
    /// A webhook csak ÉSZLELI a hiányt (lásd GitWebhookService), a pótlás innen indul - azért
    /// külön kérésből, mert a szolgáltató API-jának megkérdezése a webhook válaszidejét a
    /// szolgáltató válaszidejéhez kötné, a lassú webhookot pedig mindkét szolgáltató hibásnak
    /// jelöli, és végül kikapcsolja.
    /// </summary>
    public class GitBackfillService : IGitBackfillService
    {
        /// <summary>
        /// Ennyi pótlás indítható egy projektben ebben az időablakban. Ez az egyetlen pont,
        /// ahol a rendszer kifelé hív, és egy türelmetlen kattintássorozat a szolgáltatói
        /// kvótát égetné el - azt a kvótát, amely a következő valódi pótláshoz kell.
        /// </summary>
        private const int MaxRunsPerWindow = 10;
        private static readonly TimeSpan RateLimitWindow = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Ennyi idő után a foglalás elévül. Enélkül egy megszakadt futás (újraindítás,
        /// telepítés) örökre InProgress állapotban hagyná a sort, és SENKI nem tudná
        /// újraindítani. Ugyanaz a gondolat, mint a token rotáció türelmi ablakánál, csak
        /// ellenkező irányba: ott a friss állapot volt a kivétel, itt az elévült.
        /// </summary>
        public static readonly TimeSpan ClaimExpiry = TimeSpan.FromMinutes(10);

        /// <summary>Ennyi kísérlet után a hiba már nem véletlen, és emberi beavatkozást kér.</summary>
        private const int RepeatedFailureThreshold = 3;

        private const int MaxErrorLength = 500;

        private readonly AppDbContext _context;
        private readonly IEnumerable<IGitCommitFetcher> _fetchers;
        private readonly IGitWebhookService _gitWebhookService;
        private readonly IEncryptionService _encryptionService;
        private readonly IRateLimitService _rateLimitService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IActivityService _activityService;
        private readonly IHubContext<ProjectHub> _hubContext;
        private readonly ILogger<GitBackfillService> _logger;

        public GitBackfillService(
            AppDbContext context,
            IEnumerable<IGitCommitFetcher> fetchers,
            IGitWebhookService gitWebhookService,
            IEncryptionService encryptionService,
            IRateLimitService rateLimitService,
            ICurrentUserService currentUserService,
            IActivityService activityService,
            IHubContext<ProjectHub> hubContext,
            ILogger<GitBackfillService> logger)
        {
            _context = context;
            _fetchers = fetchers;
            _gitWebhookService = gitWebhookService;
            _encryptionService = encryptionService;
            _rateLimitService = rateLimitService;
            _currentUserService = currentUserService;
            _activityService = activityService;
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task<List<CommitBackfillResponseDto>> GetBackfillsAsync(Guid projectId)
        {
            var backfills = await _context.CommitBackfills
                .Include(b => b.Integration)
                .Where(b => b.Integration.ProjectId == projectId)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            return backfills.Select(MapToDto).ToList();
        }

        public async Task<CommitBackfillResponseDto> RunBackfillAsync(Guid projectId, Guid backfillId)
        {
            //A korlát a sor megkeresése ELŐTT fut, ugyanazzal a megfontolással, mint a webhook
            //végponton: különben a kattintássorozat az adatbázist is terhelné.
            var (isLimited, retryAfter) = await _rateLimitService.IsRateLimitedAsync(
                $"git-backfill:{projectId}", MaxRunsPerWindow, RateLimitWindow);

            if (isLimited)
                throw new RateLimitException(
                    $"Túl sok pótlás indult ebben a projektben. Próbáld újra {retryAfter} másodperc múlva!");

            var backfill = await _context.CommitBackfills
                .Include(b => b.Integration)
                .FirstOrDefaultAsync(b => b.Id == backfillId && b.Integration.ProjectId == projectId);

            if (backfill == null)
                throw new NotFoundException("A pótlandó push nem található!");

            var fetcher = EnsureRunnable(backfill);

            await ClaimAsync(backfill.Id, _currentUserService.UserId);

            //A foglalás az adatbázisban történt, a követett példány még a régi állapotot hiszi
            await _context.Entry(backfill).ReloadAsync();

            return await ExecuteAsync(projectId, backfill, fetcher, automatic: false);
        }

        public async Task RunAutomaticAsync(Guid backfillId)
        {
            var backfill = await _context.CommitBackfills
                .Include(b => b.Integration)
                .FirstOrDefaultAsync(b => b.Id == backfillId);

            if (backfill == null)
            {
                _logger.LogWarning(
                    "Automatikus pótlás: a nyilvántartási sor eltűnt | BackfillId: {BackfillId}", backfillId);
                return;
            }

            var integration = backfill.Integration;

            //A kapcsolót a várólistára tétel ÓTA is kikapcsolhatták: a döntés pillanata az
            //indítás, nem az észlelés
            if (!integration.AutoBackfillEnabled)
            {
                _logger.LogInformation(
                    "Automatikus pótlás elmarad: az integráción közben kikapcsolták | IntegrationId: {IntegrationId} | BackfillId: {BackfillId}",
                    integration.Id, backfillId);
                return;
            }

            //Itt nincs kinek kivételt dobni: a hívó egy háttérfutás. Ezért minden előfeltétel
            //naplóba kerül, a sor pedig Pending marad - vagyis a felhasználó kézzel indíthatja.
            if (backfill.BeforeSha == null || backfill.AfterSha == null)
            {
                _logger.LogWarning(
                    "Automatikus pótlás elmarad: a push azonosítói hiányoznak | BackfillId: {BackfillId}", backfillId);
                return;
            }

            if (!integration.IsEnabled)
            {
                _logger.LogWarning(
                    "Automatikus pótlás elmarad: az integráció le van tiltva | IntegrationId: {IntegrationId}",
                    integration.Id);
                return;
            }

            var fetcher = _fetchers.FirstOrDefault(f => f.Provider == integration.Provider);
            if (fetcher == null)
            {
                _logger.LogError(
                    "Automatikus pótlás elmarad: nincs commit lekérő a providerhez | Provider: {Provider}",
                    integration.Provider);
                return;
            }

            //Ugyanaz a korlát, mint a kézi indításnál: egy hibás szolgáltatói állapotból fakadó
            //csonkolás-sorozat különben kvótát égetne - azt a kvótát, amely a következő valódi
            //pótláshoz kell. Korlátozásnál a sor Pending marad, tehát nem veszik el.
            var (isLimited, _) = await _rateLimitService.IsRateLimitedAsync(
                $"git-backfill:{integration.ProjectId}", MaxRunsPerWindow, RateLimitWindow);

            if (isLimited)
            {
                _logger.LogWarning(
                    "Automatikus pótlás elmarad: elérte a projekt kérésszám-korlátját, a sor kézzel indítható | ProjectId: {ProjectId} | BackfillId: {BackfillId}",
                    integration.ProjectId, backfillId);
                return;
            }

            if (!await TryClaimAutomaticallyAsync(backfillId))
            {
                //Vagy már fut, vagy valaki megelőzött minket - mindkettő rendben van
                _logger.LogInformation(
                    "Automatikus pótlás elmarad: a sor már nem érintetlen | BackfillId: {BackfillId}", backfillId);
                return;
            }

            await _context.Entry(backfill).ReloadAsync();

            await ExecuteAsync(integration.ProjectId, backfill, fetcher, automatic: true);
        }

        /// <summary>
        /// Foglalás az automatikus kísérlethez. Két dologban szigorúbb a kézinél:
        ///
        /// - a <c>ClaimedById</c> üresen marad, ami a rendszerindítást jelöli;
        /// - a feltétel <b>érintetlen</b> sort kíván (<c>Pending</c> és nulla kísérlet), így a
        ///   „pontosan egyszer" nem a hívási hely véletlenjén múlik, hanem kikényszerített.
        ///
        /// Ha a felhasználó ugyanebben a pillanatban nyomja meg a gombot, a vesztes ág itt
        /// nulla sort frissít, és dolga sincs tovább - második lekérés nem indul.
        /// </summary>
        private async Task<bool> TryClaimAutomaticallyAsync(Guid backfillId)
        {
            var now = DateTime.UtcNow;

            var claimed = await _context.CommitBackfills
                .Where(b => b.Id == backfillId
                            && b.State == BackfillStates.Pending
                            && b.AttemptCount == 0)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(b => b.State, BackfillStates.InProgress)
                    .SetProperty(b => b.ClaimedAt, now)
                    .SetProperty(b => b.ClaimedById, (Guid?)null)
                    .SetProperty(b => b.AttemptCount, b => b.AttemptCount + 1));

            return claimed > 0;
        }

        /// <summary>
        /// A pótlás előfeltételei. Mind a négy esetben a KÉRÉS nem teljesíthető, nem a
        /// szolgáltató hibázott - ezért kivétel, és nem Failed állapot.
        /// </summary>
        private IGitCommitFetcher EnsureRunnable(CommitBackfill backfill)
        {
            if (backfill.State == BackfillStates.Completed)
                throw new ValidationException("Ez a push már pótolva van!");

            //Azonosítók nélkül nincs mihez képest lekérdezni: a compare végpont pont ezt a két
            //azonosítót kapja paraméterként
            if (backfill.BeforeSha == null || backfill.AfterSha == null)
                throw new ValidationException(
                    "Ez a push nem pótolható: a szolgáltató nem küldött azonosítót hozzá!");

            if (!backfill.Integration.IsEnabled)
                throw new ValidationException(
                    "Az integráció le van tiltva, ezért nem indítunk felé kimenő kérést!");

            var fetcher = _fetchers.FirstOrDefault(f => f.Provider == backfill.Integration.Provider);
            if (fetcher == null)
                throw new ValidationException(
                    $"Ehhez a szolgáltatóhoz nincs commit lekérő: {backfill.Integration.Provider}");

            return fetcher;
        }

        /// <summary>
        /// A sor lefoglalása. Ez a minta a projektben már kétszer előállt - a refresh token
        /// rotációjánál és a feltöltés megerősítésénél -, és ez a harmadik, egymástól független
        /// előfordulása: <b>feltételes UPDATE, és a döntés az érintett sorok számából születik.</b>
        ///
        /// Enélkül két egyidejű indítás két lapozó lekérést futtatna ugyanarra az intervallumra:
        /// dupla kvótafogyás és egymásba futó beillesztések. Az ellenőrzés-majd-írás itt nem
        /// elég, mert a kettő között épp a másik kérés írna.
        ///
        /// A Failed és a Partial állapot szándékosan foglalható: egyik sem végállapot.
        /// </summary>
        private async Task ClaimAsync(Guid backfillId, Guid? userId)
        {
            var now = DateTime.UtcNow;
            var staleBefore = now - ClaimExpiry;

            var claimed = await _context.CommitBackfills
                .Where(b => b.Id == backfillId
                            && (b.State == BackfillStates.Pending
                                || b.State == BackfillStates.Failed
                                || b.State == BackfillStates.Partial
                                || (b.State == BackfillStates.InProgress
                                    && b.ClaimedAt != null
                                    && b.ClaimedAt < staleBefore)))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(b => b.State, BackfillStates.InProgress)
                    .SetProperty(b => b.ClaimedAt, now)
                    .SetProperty(b => b.ClaimedById, userId)
                    .SetProperty(b => b.AttemptCount, b => b.AttemptCount + 1));

            if (claimed == 0)
                throw new ConflictException("A pótlás már folyamatban van, várd meg, amíg befejeződik!");
        }

        /// <summary>
        /// A lefoglalt sor végrehajtása: lekérés, beillesztés, zárás. A hívó addig birtokolja a
        /// sort, amíg ez le nem fut - vagy amíg a foglalás el nem évül.
        /// </summary>
        private async Task<CommitBackfillResponseDto> ExecuteAsync(
            Guid projectId, CommitBackfill backfill, IGitCommitFetcher fetcher, bool automatic)
        {
            var integration = backfill.Integration;
            var stopwatch = Stopwatch.StartNew();

            string? token;
            try
            {
                token = integration.AccessToken is { } stored ? _encryptionService.Decrypt(stored) : null;
            }
            catch (Exception ex)
            {
                //Megváltozott titkosítókulcs vagy sérült érték. A felhasználó ebből azt tudja
                //meg, amit tehet: állítsa be újra a tokent.
                _logger.LogError(ex,
                    "A tárolt hozzáférési token nem fejthető vissza | IntegrationId: {IntegrationId}",
                    integration.Id);

                return await FinishWithFailureAsync(projectId, backfill, automatic, stopwatch,
                    GitFetchErrorKind.Authentication,
                    "A tárolt hozzáférési token nem használható. Állítsd be újra!");
            }

            var outcome = await fetcher.FetchRangeAsync(new GitFetchRequest(
                RepoFullName: integration.RepoFullName,
                InstanceUrl: integration.InstanceUrl,
                AccessToken: token,
                BeforeSha: backfill.BeforeSha!,
                AfterSha: backfill.AfterSha!));

            if (!outcome.Success)
                return await FinishWithFailureAsync(projectId, backfill, automatic, stopwatch,
                    outcome.ErrorKind!.Value, outcome.ErrorMessage!);

            //A lekért commitok UGYANAZON az úton mennek át, mint a webhookéi: illesztés,
            //kapcsolatlétrehozás, tevékenységnapló, valós idejű esemény. Ez a rész
            //újrahasznosítás, nem új kód - és a duplikátumot az egyedi index zárja ki,
            //tehát a már meglévő commitok újbóli behúzása sem okoz hibát.
            await _gitWebhookService.ProcessPushEventAsync(
                projectId, integration.Id, integration.Provider,
                new GitPushEvent(outcome.Commits, SkippedCommitCount: 0));

            stopwatch.Stop();

            backfill.State = outcome.Incomplete ? BackfillStates.Partial : BackfillStates.Completed;
            backfill.CompletedAt = DateTime.UtcNow;

            //Részeredménynél a mezőben nem hiba, hanem a hiány MARADÉKÁNAK az oka áll
            backfill.LastError = outcome.Incomplete
                ? "A szolgáltató sem adta meg a teljes intervallumot, a hiány csökkent, de nem szűnt meg."
                : null;

            await _context.SaveChangesAsync();

            LogAttempt(backfill, integration, automatic, stopwatch.ElapsedMilliseconds,
                outcome.Commits.Count, kind: null, succeeded: true, incomplete: outcome.Incomplete);

            await LogActivityAsync(
                projectId, backfill, integration, outcome.Commits.Count, outcome.Incomplete, automatic);
            await BroadcastAsync(projectId, backfill);

            return MapToDto(backfill);
        }

        private async Task<CommitBackfillResponseDto> FinishWithFailureAsync(
            Guid projectId, CommitBackfill backfill, bool automatic, Stopwatch stopwatch,
            GitFetchErrorKind kind, string message)
        {
            stopwatch.Stop();

            //A Failed nem végállapot: a foglalás feloldódik, tehát a sor újraindítható
            backfill.State = BackfillStates.Failed;
            backfill.LastError = message.Length > MaxErrorLength
                ? message[..MaxErrorLength]
                : message;

            await _context.SaveChangesAsync();

            LogAttempt(backfill, backfill.Integration, automatic, stopwatch.ElapsedMilliseconds,
                commitCount: 0, kind, succeeded: false, incomplete: false);

            await BroadcastAsync(projectId, backfill);

            return MapToDto(backfill);
        }

        /// <summary>
        /// A kísérlet naplózása.
        ///
        /// Azért kell a nyilvántartási sor MELLETT is: a sor a felhasználónak szól, egy projekt
        /// egy pushát mutatja, és egy későbbi sikeres futás felülírja. Az üzemeltető viszont
        /// azt kérdezi, hogy a pótlás általában működik-e - és ha az automatikus kísérletek
        /// rendre hibára futnak, azt hibaüzenet nélkül SEMMI nem mutatná meg. Ez a funkció
        /// legvalószínűbb csendes meghibásodása.
        /// </summary>
        private void LogAttempt(
            CommitBackfill backfill, Integration integration, bool automatic, long elapsedMs,
            int commitCount, GitFetchErrorKind? kind, bool succeeded, bool incomplete)
        {
            //A sikert sem hallgatjuk el: a pótlás definíció szerint rendellenesség, csak akkor
            //van, ha előtte elveszett valami. A hitelesítési hiba és az ismétlődő hiba viszont
            //emberi beavatkozást kér, ezért hangosabb.
            var level = (succeeded, kind, backfill.AttemptCount) switch
            {
                (true, _, _) when incomplete => LogLevel.Warning,
                (true, _, _) => LogLevel.Information,
                (false, GitFetchErrorKind.Authentication, _) => LogLevel.Error,
                (false, _, >= RepeatedFailureThreshold) => LogLevel.Error,
                _ => LogLevel.Warning
            };

            _logger.Log(level,
                "Commit pótlás | Eredmény: {Result} | Indító: {Trigger} | Hiba: {ErrorKind} | " +
                "Behúzva: {CommitCount} | Ígért: {Reported} | Érkezett: {Received} | Kísérlet: {Attempt} | " +
                "Idő: {ElapsedMs} ms | Provider: {Provider} | Ref: {Ref} | " +
                "IntegrationId: {IntegrationId} | BackfillId: {BackfillId}",
                succeeded ? (incomplete ? "részleges" : "siker") : "hiba",
                automatic ? "automatikus" : "felhasználó",
                kind?.ToString() ?? "-",
                commitCount,
                backfill.ReportedCommitCount?.ToString() ?? "ismeretlen",
                backfill.ReceivedCommitCount,
                backfill.AttemptCount,
                elapsedMs,
                integration.Provider,
                backfill.Ref,
                integration.Id,
                backfill.Id);
        }

        private async Task LogActivityAsync(
            Guid projectId, CommitBackfill backfill, Integration integration,
            int commitCount, bool incomplete, bool automatic)
        {
            try
            {
                var where = integration.RepoFullName +
                            (backfill.Ref == null ? string.Empty : $", {backfill.Ref}");

                var remainder = incomplete ? " — a hiány csökkent, de nem szűnt meg" : string.Empty;

                //Az automatikus kísérletnek NINCS cselekvője: a háttérfutásban nincs
                //bejelentkezett felhasználó, és egy {actor} helyőrző itt kitöltetlen maradna.
                //Ugyanaz a megfontolás, mint a webhook ágán.
                var activity = automatic
                    ? await _activityService.LogSystemActivityAsync(
                        projectId, "Integration", integration.Id, "CommitsBackfilled",
                        $"Automatikus pótlás ({where}): {commitCount} commit{remainder}")
                    : await _activityService.LogActivityAsync(
                        projectId, "Integration", integration.Id, "CommitsBackfilled",
                        $"{{actor}} pótolta a kimaradt commitokat ({where}): {commitCount} commit{remainder}");

                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("ActivityCreated", activity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: ActivityCreated | ProjectId: {ProjectId}", projectId);
            }
        }

        private async Task BroadcastAsync(Guid projectId, CommitBackfill backfill)
        {
            try
            {
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("BackfillUpdated", MapToDto(backfill));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                    "BackfillUpdated", projectId);
            }
        }

        private static CommitBackfillResponseDto MapToDto(CommitBackfill backfill) => new()
        {
            Id = backfill.Id,
            IntegrationId = backfill.IntegrationId,
            Provider = backfill.Integration.Provider,
            RepoFullName = backfill.Integration.RepoFullName,
            Ref = backfill.Ref,
            ReportedCommitCount = backfill.ReportedCommitCount,
            ReceivedCommitCount = backfill.ReceivedCommitCount,
            State = backfill.State,
            AttemptCount = backfill.AttemptCount,
            LastError = backfill.LastError,

            CanRun = backfill.State != BackfillStates.Completed
                     && backfill.BeforeSha != null
                     && backfill.AfterSha != null
                     && backfill.Integration.IsEnabled,

            //Lefoglalva, de felhasználó nélkül: ezt csak a rendszer tehette. A soha nem
            //foglalt sor azonosítója is üres, ezért a foglalás idejét is meg kell kérdezni.
            LastAttemptWasAutomatic = backfill.ClaimedAt != null && backfill.ClaimedById == null,

            CreatedAt = backfill.CreatedAt,
            ClaimedAt = backfill.ClaimedAt,
            CompletedAt = backfill.CompletedAt
        };
    }
}
