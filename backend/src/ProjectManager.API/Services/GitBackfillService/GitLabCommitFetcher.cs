using ProjectManager.API.Common.Constants;
using ProjectManager.API.Services.GitWebhookService.Payloads;
using System.Text.Json;

namespace ProjectManager.API.Services.GitBackfillService
{
    /// <summary>
    /// Commitok lekérése a GitLab compare végpontjáról.
    /// Dokumentáció: https://docs.gitlab.com/api/repositories/
    ///
    /// Három dologban tér el a GitHubtól, és mindhárom látszik a kódon:
    /// 1. a végpont NEM lapozható - egy válaszban adja az intervallumot;
    /// 2. hitelesítés nélkül a commitlistát a szolgáltató 100 elemre vágja;
    /// 3. a válasz saját mezőt hordoz arról, hogy az összehasonlítás időtúllépett-e.
    /// (Ellenőrizve a dokumentációban: 2026. 11. 18.)
    ///
    /// A saját telepítésű (self-managed) GitLab itt a tipikus eset, nem a kivétel: a céges
    /// példány címe nem a gitlab.com, és a pótlás abba az irányba indul.
    /// </summary>
    public sealed class GitLabCommitFetcher : IGitCommitFetcher
    {
        private const string CloudRoot = "https://gitlab.com/api/v4";
        private const string VersionPath = "/api/v4";

        /// <summary>
        /// Amennyi commitot a szolgáltató hitelesítés nélkül legfeljebb ad. Pontosan ennyi
        /// commit mellett tehát nem tudjuk, vége van-e az intervallumnak - a hiányt ilyenkor
        /// jelezni kell, nem elhallgatni.
        /// </summary>
        private const int UnauthenticatedLimit = 100;

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<GitLabCommitFetcher> _logger;

        public GitLabCommitFetcher(IHttpClientFactory httpClientFactory, ILogger<GitLabCommitFetcher> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public string Provider => GitProviders.GitLab;

        public async Task<GitFetchOutcome> FetchRangeAsync(
            GitFetchRequest request, CancellationToken cancellationToken = default)
        {
            var root = GitApiRoot.Resolve(request.InstanceUrl, CloudRoot, VersionPath);
            var client = _httpClientFactory.CreateClient(GitApiHttpClient.Name);

            //A tárház azonosítója útvonalrészként utazik, tehát a csoport/alcsoport/projekt
            //alakban lévő perjeleket kódolni KELL - enélkül a szolgáltató más végpontot látna
            var projectPath = Uri.EscapeDataString(request.RepoFullName);

            //A straight=true a két állapot KÖZVETLEN összehasonlítása. Enélkül a szolgáltató a
            //közös őstől (merge base) számol, és a push során érkezett commitok helyett egy
            //másik halmazt adna vissza.
            var url = $"{root}/projects/{projectPath}/repository/compare" +
                      $"?from={request.BeforeSha}&to={request.AfterSha}&straight=true";

            using var message = new HttpRequestMessage(HttpMethod.Get, url);

            //A személyes hozzáférési token saját fejlécben utazik, nem Bearerként
            if (!string.IsNullOrEmpty(request.AccessToken))
                message.Headers.Add("PRIVATE-TOKEN", request.AccessToken);

            JsonDocument document;
            try
            {
                using var response = await client.SendAsync(message, cancellationToken);

                var failure = GitFetchErrorClassifier.Classify(response);
                if (failure != null) return failure;

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                document = JsonDocument.Parse(body);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return GitFetchOutcome.Fail(GitFetchErrorKind.Transient,
                    "A szolgáltató nem válaszolt időben.");
            }
            catch (HttpRequestException ex)
            {
                //Saját telepítésnél ez a gyakori hiba: elírt vagy belső hálózatból nem elérhető cím
                _logger.LogWarning(ex, "A GitLab API nem elérhető | Root: {Root}", root);
                return GitFetchOutcome.Fail(GitFetchErrorKind.Transient,
                    "A szolgáltató nem elérhető. Saját telepítés esetén ellenőrizd a kiszolgáló címét.");
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "A GitLab API válasza nem értelmezhető JSON");
                return GitFetchOutcome.Fail(GitFetchErrorKind.Permanent,
                    "A szolgáltató válasza nem értelmezhető.");
            }

            using (document)
            {
                var payload = document.RootElement;

                if (!payload.TryReadArray("commits", out var array))
                    return GitFetchOutcome.Fail(GitFetchErrorKind.Permanent,
                        "A szolgáltató válasza nem tartalmazott commitlistát.");

                var commits = ReadCommits(array);

                //A szolgáltató maga mondja meg, ha az összehasonlítás nem ért végig
                var timedOut = payload.ReadBool("compare_timeout");

                //Token nélkül a 100. commitnál nem tudjuk, van-e még: a határ pont ennyi
                var cappedByMissingToken =
                    string.IsNullOrEmpty(request.AccessToken) && commits.Count >= UnauthenticatedLimit;

                if (timedOut || cappedByMissingToken)
                {
                    _logger.LogWarning(
                        "A GitLab commit lekérés nem adta meg a teljes intervallumot | Időtúllépés: {TimedOut} | Token nélküli korlát: {Capped} | Commitok: {Count}",
                        timedOut, cappedByMissingToken, commits.Count);
                }

                return GitFetchOutcome.Ok(commits, incomplete: timedOut || cappedByMissingToken);
            }
        }

        /// <summary>
        /// A compare végpont alakja a webhook payloadjától itt is eltér: a szerző adatai lapos
        /// mezőkben jönnek (author_name, author_email), az idő pedig created_at.
        ///
        /// Az üzenetnél a message az elsődleges, a title a tartalék: a dokumentáció a
        /// commitlistánál a title mezőt nevesíti, a feladatkulcs viszont a teljes üzenet
        /// törzsében is állhat, tehát a bővebb mezőt kell előnyben részesíteni.
        /// </summary>
        private static List<GitCommitInfo> ReadCommits(JsonElement array)
        {
            var commits = new List<GitCommitInfo>();

            foreach (var element in array.EnumerateArray())
            {
                if (!element.TryReadString("id", out var sha) || sha.Length == 0) continue;

                var message = element.ReadOptionalString("message")
                              ?? element.ReadOptionalString("title");
                if (message == null) continue;

                commits.Add(new GitCommitInfo(
                    Sha: sha,
                    Message: message,

                    //A commitlista elemei nem hordoznak saját webes címet: a válasz web_url
                    //mezője az összehasonlításra mutat, nem az egyes commitokra. A hivatkozás
                    //üresen hagyása jobb, mint egy kitalált cím - a mező nullozható.
                    Url: element.ReadOptionalString("web_url"),
                    AuthorName: element.ReadOptionalString("author_name") ?? string.Empty,
                    AuthorEmail: element.ReadOptionalString("author_email") ?? string.Empty,
                    CommittedAt: element.ReadTimestamp("created_at") ?? DateTime.UtcNow));
            }

            return commits;
        }
    }
}
