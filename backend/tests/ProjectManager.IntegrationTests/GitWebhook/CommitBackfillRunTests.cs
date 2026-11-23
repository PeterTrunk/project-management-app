using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.Data;
using ProjectManager.API.Common.Exceptions;
using ProjectManager.API.Model;
using ProjectManager.API.Services.GitBackfillService;
using ProjectManager.API.Services.GitWebhookService.Payloads;
using ProjectManager.IntegrationTests.Infrastructure;

namespace ProjectManager.IntegrationTests.GitWebhook
{
    /// <summary>
    /// A kimaradt commitok pótlása.
    ///
    /// A webhook csak észleli a hiányt, a pótlás külön kérésből indul - a szolgáltató API-jának
    /// megkérdezése ugyanis a webhook válaszidejét a szolgáltató válaszidejéhez kötné, a lassú
    /// webhookot pedig mindkét szolgáltató hibásnak jelöli, és végül kikapcsolja.
    ///
    /// A tesztek három dolgot néznek: a foglalás kizárja-e a párhuzamos indítást, a lekért
    /// commitok ugyanazon az illesztő úton mennek-e át, mint a webhookéi, és a nyilvántartási
    /// sor állapota hűen tükrözi-e a kimenetet (beleértve a RÉSZLEGES pótlást).
    /// </summary>
    public class CommitBackfillRunTests : DatabaseTestBase
    {
        public CommitBackfillRunTests(PostgresFixture fixture) : base(fixture) { }

        private const string Before = "1111111111111111111111111111111111111111";
        private const string After = "2222222222222222222222222222222222222222";
        private const string Sha = "aabbccddeeff00112233445566778899aabbccdd";

        private static GitCommitInfo Commit(string message, string sha = Sha) =>
            new(Sha: sha,
                Message: message,
                Url: "https://github.com/peter/pma/commit/" + sha[..7],
                AuthorName: "Teszt Elek",
                AuthorEmail: "teszt@example.com",
                CommittedAt: new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc));

        private static async Task<CommitBackfill> SeedBackfillAsync(
            AppDbContext context, Guid integrationId,
            string? beforeSha = Before, string? afterSha = After,
            string state = BackfillStates.Pending,
            DateTime? claimedAt = null, Guid? claimedById = null, int attemptCount = 0)
        {
            var backfill = new CommitBackfill
            {
                Id = Guid.NewGuid(),
                IntegrationId = integrationId,
                Ref = "refs/heads/main",
                BeforeSha = beforeSha,
                AfterSha = afterSha,
                ReportedCommitCount = 25,
                ReceivedCommitCount = 20,
                State = state,
                ClaimedAt = claimedAt,
                ClaimedById = claimedById,
                AttemptCount = attemptCount
            };

            context.CommitBackfills.Add(backfill);
            await context.SaveChangesAsync();

            return backfill;
        }

        private async Task<CommitBackfill> ReloadAsync(Guid backfillId)
        {
            await using var verify = CreateContext();
            return await verify.CommitBackfills.SingleAsync(b => b.Id == backfillId);
        }

        private async Task<List<CommitLink>> CommitLinksAsync(Guid integrationId)
        {
            await using var verify = CreateContext();
            return await verify.CommitLinks
                .Where(cl => cl.IntegrationId == integrationId)
                .ToListAsync();
        }

        [Fact]
        public async Task SuccessfulRun_LinksTheCommitsAndClosesTheRow()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var backfill = await SeedBackfillAsync(context, integration.Id);

            var fetcher = new StubGitCommitFetcher
            {
                Outcome = GitFetchOutcome.Ok([Commit("AAA-1 kimaradt commit")])
            };
            var (sut, ctx) = ServiceFactory.CreateGitBackfillService(context, seed.Owner.Id, fetcher);

            var response = await sut.RunBackfillAsync(seed.Project.Id, backfill.Id);

            //A commit ugyanazon az illesztő úton ment át, mint a webhookéi
            var link = Assert.Single(await CommitLinksAsync(integration.Id));
            Assert.Equal(seed.Task.Id, link.TaskId);
            Assert.False(link.IsManuallyLinked);

            var stored = await ReloadAsync(backfill.Id);
            Assert.Equal(BackfillStates.Completed, stored.State);
            Assert.NotNull(stored.CompletedAt);
            Assert.Equal(1, stored.AttemptCount);
            Assert.Null(stored.LastError);

            Assert.Equal(BackfillStates.Completed, response.State);
            Assert.False(response.CanRun);
            Assert.False(response.LastAttemptWasAutomatic);

            Assert.True(ctx.Hub.SentToProject(seed.Project.Id, "BackfillUpdated"));
            Assert.True(ctx.Hub.SentToProject(seed.Project.Id, "CommitLinked"));

            await using var verify = CreateContext();
            Assert.True(await verify.Activities.AnyAsync(a => a.Action == "CommitsBackfilled"));
        }

        /// <summary>
        /// A lekérés a TÁROLT intervallumot kapja meg, és a visszafejtett tokent. Ezért kell a
        /// két SHA-t eltárolni: a gomb később, egy másik kérésben indul, amikor a payload már
        /// nincs meg.
        /// </summary>
        [Fact]
        public async Task TheFetch_GetsTheStoredRangeAndTheDecryptedToken()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var backfill = await SeedBackfillAsync(context, integration.Id);

            var fetcher = new StubGitCommitFetcher();
            var (sut, ctx) = ServiceFactory.CreateGitBackfillService(context, seed.Owner.Id, fetcher);

            integration.AccessToken = ctx.Encryption.Encrypt("ghp_titok");
            integration.InstanceUrl = "https://ghe.ceg.hu";
            await context.SaveChangesAsync();

            await sut.RunBackfillAsync(seed.Project.Id, backfill.Id);

            var request = Assert.Single(fetcher.Requests);
            Assert.Equal(Before, request.BeforeSha);
            Assert.Equal(After, request.AfterSha);
            Assert.Equal("peter/pma", request.RepoFullName);
            Assert.Equal("https://ghe.ceg.hu", request.InstanceUrl);

            //A titok visszafejtve megy ki, de csak a kimenő hívásba
            Assert.Equal("ghp_titok", request.AccessToken);
        }

        [Fact]
        public async Task WithoutAStoredToken_TheFetchGetsNone()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var backfill = await SeedBackfillAsync(context, integration.Id);

            var fetcher = new StubGitCommitFetcher();
            var (sut, _) = ServiceFactory.CreateGitBackfillService(context, seed.Owner.Id, fetcher);

            await sut.RunBackfillAsync(seed.Project.Id, backfill.Id);

            Assert.Null(Assert.Single(fetcher.Requests).AccessToken);
        }

        /// <summary>
        /// A párhuzamosság kizárása. Enélkül két egyidejű indítás két lapozó lekérést futtatna
        /// ugyanarra az intervallumra: dupla kvótafogyás és egymásba futó beillesztések.
        /// </summary>
        [Fact]
        public async Task WhileARunIsInProgress_ASecondOneIsRejected()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var backfill = await SeedBackfillAsync(
                context, integration.Id,
                state: BackfillStates.InProgress,
                claimedAt: DateTime.UtcNow,
                claimedById: seed.Owner.Id,
                attemptCount: 1);

            var fetcher = new StubGitCommitFetcher();
            var (sut, _) = ServiceFactory.CreateGitBackfillService(context, seed.Owner.Id, fetcher);

            await Assert.ThrowsAsync<ConflictException>(() =>
                sut.RunBackfillAsync(seed.Project.Id, backfill.Id));

            //A vesztes ág nem indít kimenő hívást: ez a kvóta védelme
            Assert.Equal(0, fetcher.Calls);
            Assert.Equal(1, (await ReloadAsync(backfill.Id)).AttemptCount);
        }

        /// <summary>
        /// A foglalásnak lejárata van. Enélkül egy megszakadt futás (újraindítás, telepítés)
        /// örökre InProgress állapotban hagyná a sort, és SENKI nem tudná újraindítani -
        /// a funkció csendben használhatatlanná válna.
        /// </summary>
        [Fact]
        public async Task AnExpiredClaim_CanBeTakenOver()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var backfill = await SeedBackfillAsync(
                context, integration.Id,
                state: BackfillStates.InProgress,
                claimedAt: DateTime.UtcNow - GitBackfillService.ClaimExpiry - TimeSpan.FromMinutes(1),
                claimedById: seed.Owner.Id,
                attemptCount: 1);

            var fetcher = new StubGitCommitFetcher
            {
                Outcome = GitFetchOutcome.Ok([Commit("AAA-1 kimaradt commit")])
            };
            var (sut, _) = ServiceFactory.CreateGitBackfillService(context, seed.Owner.Id, fetcher);

            var response = await sut.RunBackfillAsync(seed.Project.Id, backfill.Id);

            Assert.Equal(BackfillStates.Completed, response.State);
            Assert.Equal(2, (await ReloadAsync(backfill.Id)).AttemptCount);
        }

        [Fact]
        public async Task AFailedFetch_MarksTheRowFailedAndLeavesItRunnable()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var backfill = await SeedBackfillAsync(context, integration.Id);

            var fetcher = new StubGitCommitFetcher
            {
                Outcome = GitFetchOutcome.Fail(
                    GitFetchErrorKind.Authentication, "A szolgáltató elutasította a hitelesítő adatot.")
            };
            var (sut, ctx) = ServiceFactory.CreateGitBackfillService(context, seed.Owner.Id, fetcher);

            //A szolgáltató hibája NEM kivétel: a felhasználónak látnia kell az okot a soron
            var response = await sut.RunBackfillAsync(seed.Project.Id, backfill.Id);

            Assert.Equal(BackfillStates.Failed, response.State);
            Assert.Contains("hitelesítő", response.LastError);
            Assert.True(response.CanRun);

            var stored = await ReloadAsync(backfill.Id);
            Assert.Equal(BackfillStates.Failed, stored.State);
            Assert.Null(stored.CompletedAt);
            Assert.Equal(1, stored.AttemptCount);

            Assert.True(ctx.Hub.SentToProject(seed.Project.Id, "BackfillUpdated"));
            Assert.Empty(await CommitLinksAsync(integration.Id));
        }

        [Fact]
        public async Task AFailedRow_CanBeRetried()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var backfill = await SeedBackfillAsync(
                context, integration.Id, state: BackfillStates.Failed, attemptCount: 1);

            var fetcher = new StubGitCommitFetcher
            {
                Outcome = GitFetchOutcome.Ok([Commit("AAA-1 kimaradt commit")])
            };
            var (sut, _) = ServiceFactory.CreateGitBackfillService(context, seed.Owner.Id, fetcher);

            var response = await sut.RunBackfillAsync(seed.Project.Id, backfill.Id);

            Assert.Equal(BackfillStates.Completed, response.State);
            Assert.Equal(2, (await ReloadAsync(backfill.Id)).AttemptCount);
        }

        /// <summary>
        /// Ha a lekérés sem adta meg a teljes intervallumot, az nem siker és nem is hiba:
        /// a hiány CSÖKKENT, de nem szűnt meg. Ezt ki kell mondani, különben a pótlás ugyanazt
        /// a néma csonkolást termelné újra, amely ellen készült.
        /// </summary>
        [Fact]
        public async Task AnIncompleteFetch_IsMarkedPartialAndStaysRunnable()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var backfill = await SeedBackfillAsync(context, integration.Id);

            var fetcher = new StubGitCommitFetcher
            {
                Outcome = GitFetchOutcome.Ok([Commit("AAA-1 kimaradt commit")], incomplete: true)
            };
            var (sut, _) = ServiceFactory.CreateGitBackfillService(context, seed.Owner.Id, fetcher);

            var response = await sut.RunBackfillAsync(seed.Project.Id, backfill.Id);

            Assert.Equal(BackfillStates.Partial, response.State);
            Assert.Contains("nem szűnt meg", response.LastError);
            Assert.True(response.CanRun);

            //A megkapott commitok ettől még bekerülnek
            Assert.Single(await CommitLinksAsync(integration.Id));
            Assert.NotNull((await ReloadAsync(backfill.Id)).CompletedAt);
        }

        /// <summary>
        /// Az idempotenciát az adatbázis adja: a CommitLink egyedi indexe (NULLS NOT DISTINCT)
        /// kizárja ugyanannak a commitnak a kétszeri beillesztését, tehát a pótlásnak nem kell
        /// külön szűrnie - és egy újraindítás nem termel duplikátumot.
        /// </summary>
        [Fact]
        public async Task RunningTwice_DoesNotDuplicateTheLinks()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var backfill = await SeedBackfillAsync(context, integration.Id);

            var fetcher = new StubGitCommitFetcher
            {
                //Részleges eredmény: a sor újraindítható marad
                Outcome = GitFetchOutcome.Ok([Commit("AAA-1 kimaradt commit")], incomplete: true)
            };
            var (sut, _) = ServiceFactory.CreateGitBackfillService(context, seed.Owner.Id, fetcher);

            await sut.RunBackfillAsync(seed.Project.Id, backfill.Id);
            await sut.RunBackfillAsync(seed.Project.Id, backfill.Id);

            Assert.Single(await CommitLinksAsync(integration.Id));
            Assert.Equal(2, (await ReloadAsync(backfill.Id)).AttemptCount);
        }

        [Fact]
        public async Task ACompletedRow_CannotBeRunAgain()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var backfill = await SeedBackfillAsync(
                context, integration.Id, state: BackfillStates.Completed);

            var fetcher = new StubGitCommitFetcher();
            var (sut, _) = ServiceFactory.CreateGitBackfillService(context, seed.Owner.Id, fetcher);

            await Assert.ThrowsAsync<ValidationException>(() =>
                sut.RunBackfillAsync(seed.Project.Id, backfill.Id));

            Assert.Equal(0, fetcher.Calls);
        }

        /// <summary>
        /// Azonosítók nélkül nincs mihez képest lekérdezni: a compare végpont pont ezt a két
        /// azonosítót kapja. A sor ilyenkor is létezik - a hiány tényét ki kell mondani -, de
        /// a pótlás nem indítható.
        /// </summary>
        [Fact]
        public async Task WithoutThePushIdentifiers_TheRunIsRejected()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var backfill = await SeedBackfillAsync(context, integration.Id, afterSha: null);

            var fetcher = new StubGitCommitFetcher();
            var (sut, _) = ServiceFactory.CreateGitBackfillService(context, seed.Owner.Id, fetcher);

            await Assert.ThrowsAsync<ValidationException>(() =>
                sut.RunBackfillAsync(seed.Project.Id, backfill.Id));

            Assert.Equal(0, fetcher.Calls);

            var rows = await sut.GetBackfillsAsync(seed.Project.Id);
            Assert.False(Assert.Single(rows).CanRun);
        }

        //Letiltott integráció felé nem indítunk kimenő kérést
        [Fact]
        public async Task ADisabledIntegration_IsNotCalled()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var backfill = await SeedBackfillAsync(context, integration.Id);

            integration.IsEnabled = false;
            await context.SaveChangesAsync();

            var fetcher = new StubGitCommitFetcher();
            var (sut, _) = ServiceFactory.CreateGitBackfillService(context, seed.Owner.Id, fetcher);

            await Assert.ThrowsAsync<ValidationException>(() =>
                sut.RunBackfillAsync(seed.Project.Id, backfill.Id));

            Assert.Equal(0, fetcher.Calls);
        }

        [Fact]
        public async Task AProviderWithoutAFetcher_IsRejected()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(
                context, seed.Project.Id, GitProviders.GitLab);
            var backfill = await SeedBackfillAsync(context, integration.Id);

            //Csak GitHub lekérő van beregisztrálva
            var fetcher = new StubGitCommitFetcher { Provider = GitProviders.GitHub };
            var (sut, _) = ServiceFactory.CreateGitBackfillService(context, seed.Owner.Id, fetcher);

            await Assert.ThrowsAsync<ValidationException>(() =>
                sut.RunBackfillAsync(seed.Project.Id, backfill.Id));

            Assert.Equal(0, fetcher.Calls);
        }

        /// <summary>
        /// A projekt hatókör a pótláson sem gyengülhet: egy másik projekt sora nem létezőnek
        /// látszik. A statisztikáknál és a git nézetnél ugyanez a szabály.
        /// </summary>
        [Fact]
        public async Task AnotherProjectsBackfill_IsNotFound()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var other = await TestData.SeedProjectAsync(context, "BBB");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var backfill = await SeedBackfillAsync(context, integration.Id);

            var fetcher = new StubGitCommitFetcher();
            var (sut, _) = ServiceFactory.CreateGitBackfillService(context, other.Owner.Id, fetcher);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                sut.RunBackfillAsync(other.Project.Id, backfill.Id));

            Assert.Equal(0, fetcher.Calls);
        }

        /// <summary>
        /// Ez az egyetlen pont, ahol a rendszer kifelé hív: egy türelmetlen kattintássorozat a
        /// szolgáltatói kvótát égetné el - azt a kvótát, amely a következő valódi pótláshoz kell.
        /// A korlát a sor megkeresése ELŐTT fut, ugyanúgy, mint a webhook végponton.
        /// </summary>
        [Fact]
        public async Task TheRunIsRateLimitedPerProject()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var backfill = await SeedBackfillAsync(context, integration.Id);

            var fetcher = new StubGitCommitFetcher();
            var (sut, ctx) = ServiceFactory.CreateGitBackfillService(context, seed.Owner.Id, fetcher);

            await sut.RunBackfillAsync(seed.Project.Id, backfill.Id);
            Assert.True(ctx.RateLimit.Asked($"git-backfill:{seed.Project.Id}"));

            ctx.RateLimit.Limit($"git-backfill:{seed.Project.Id}");

            await Assert.ThrowsAsync<RateLimitException>(() =>
                sut.RunBackfillAsync(seed.Project.Id, backfill.Id));

            Assert.Equal(1, fetcher.Calls);
        }

        /// <summary>
        /// A felületnek tudnia kell, hogy egy futó sorhoz hozzá lehet-e nyúlni. A foglalás
        /// lejárata SZERVEROLDALI szabály, ezért a sor maga hordozza a választ: egy kliensoldali
        /// másolat második igazságforrás lenne, és egy megszakadt futás örökre „folyamatban"
        /// állóként látszódna, amelyhez senki nem tud hozzányúlni.
        /// </summary>
        [Fact]
        public async Task AFreshClaimIsNotRunnable_AnExpiredOneIs()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);

            var running = await SeedBackfillAsync(
                context, integration.Id,
                state: BackfillStates.InProgress,
                claimedAt: DateTime.UtcNow,
                claimedById: seed.Owner.Id,
                attemptCount: 1);

            var stuck = await SeedBackfillAsync(
                context, integration.Id,
                afterSha: "3333333333333333333333333333333333333333",
                state: BackfillStates.InProgress,
                claimedAt: DateTime.UtcNow - GitBackfillService.ClaimExpiry - TimeSpan.FromMinutes(1),
                claimedById: seed.Owner.Id,
                attemptCount: 1);

            var (sut, _) = ServiceFactory.CreateGitBackfillService(
                context, seed.Owner.Id, new StubGitCommitFetcher());

            var rows = await sut.GetBackfillsAsync(seed.Project.Id);

            Assert.False(rows.Single(r => r.Id == running.Id).CanRun);
            Assert.True(rows.Single(r => r.Id == stuck.Id).CanRun);
        }

        [Fact]
        public async Task GetBackfills_ReturnsTheProjectsRowsNewestFirst()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var other = await TestData.SeedProjectAsync(context, "BBB");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var otherIntegration = await TestData.SeedIntegrationAsync(context, other.Project.Id);

            var older = await SeedBackfillAsync(context, integration.Id, afterSha: After);
            var newer = await SeedBackfillAsync(context, integration.Id, afterSha: "3333333333333333333333333333333333333333");
            await SeedBackfillAsync(context, otherIntegration.Id);

            //A CreatedAt-et a context állítja; a sorrendhez szét kell húzni őket
            older.CreatedAt = DateTime.UtcNow.AddHours(-2);
            await context.SaveChangesAsync();

            var fetcher = new StubGitCommitFetcher();
            var (sut, _) = ServiceFactory.CreateGitBackfillService(context, seed.Owner.Id, fetcher);

            var rows = await sut.GetBackfillsAsync(seed.Project.Id);

            Assert.Equal(2, rows.Count);
            Assert.Equal(newer.Id, rows[0].Id);
            Assert.Equal(older.Id, rows[1].Id);

            //A sor a felületnek szóló adatokat is hordozza
            Assert.Equal(GitProviders.GitHub, rows[0].Provider);
            Assert.Equal("peter/pma", rows[0].RepoFullName);
            Assert.Equal(25, rows[0].ReportedCommitCount);
            Assert.Equal(20, rows[0].ReceivedCommitCount);
            Assert.True(rows[0].CanRun);
        }
    }
}
