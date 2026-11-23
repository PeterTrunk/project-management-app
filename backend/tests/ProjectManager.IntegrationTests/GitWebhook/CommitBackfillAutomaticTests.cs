using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.Data;
using ProjectManager.API.Model;
using ProjectManager.API.Services.GitBackfillService;
using ProjectManager.API.Services.GitWebhookService.Payloads;
using ProjectManager.IntegrationTests.Infrastructure;

namespace ProjectManager.IntegrationTests.GitWebhook
{
    /// <summary>
    /// Az automatikus commit-pótlás.
    ///
    /// Két dolgot kell szétválasztva látni, mert a terv érvelése ezen áll:
    /// a webhook csak <b>várólistára tesz</b> (a lekérés a válasza után, háttérfutásban indul,
    /// különben a szolgáltató lassú webhookot látna és végül kikapcsolná), a futás pedig
    /// <b>pontosan egyszer</b> történik - automatikus ismétlés nincs, mert a tipikus hibaok
    /// (visszavont token, átnevezett tárház) nem átmeneti.
    /// </summary>
    public class CommitBackfillAutomaticTests : DatabaseTestBase
    {
        public CommitBackfillAutomaticTests(PostgresFixture fixture) : base(fixture) { }

        private const string Before = "1111111111111111111111111111111111111111";
        private const string After = "2222222222222222222222222222222222222222";

        private static GitCommitInfo Commit(string message) =>
            new(Sha: "aabbccddeeff00112233445566778899aabbccdd",
                Message: message,
                Url: null,
                AuthorName: "Teszt Elek",
                AuthorEmail: "teszt@example.com",
                CommittedAt: new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc));

        private static GitPushEvent TruncatedPush() =>
            new([Commit("AAA-1 megérkezett commit")],
                SkippedCommitCount: 0,
                ProviderReportedCommitCount: 25,
                CommitsPossiblyTruncated: true,
                BeforeSha: Before,
                AfterSha: After,
                Ref: "refs/heads/main");

        private static async Task<CommitBackfill> SeedBackfillAsync(
            AppDbContext context, Guid integrationId,
            string state = BackfillStates.Pending, int attemptCount = 0)
        {
            var backfill = new CommitBackfill
            {
                Id = Guid.NewGuid(),
                IntegrationId = integrationId,
                Ref = "refs/heads/main",
                BeforeSha = Before,
                AfterSha = After,
                ReportedCommitCount = 25,
                ReceivedCommitCount = 20,
                State = state,
                AttemptCount = attemptCount
            };

            context.CommitBackfills.Add(backfill);
            await context.SaveChangesAsync();

            return backfill;
        }

        private static async Task<Integration> SeedIntegrationAsync(
            AppDbContext context, Guid projectId, bool autoBackfill)
        {
            var integration = await TestData.SeedIntegrationAsync(context, projectId);

            integration.AutoBackfillEnabled = autoBackfill;
            await context.SaveChangesAsync();

            return integration;
        }

        private async Task<CommitBackfill> ReloadAsync(Guid backfillId)
        {
            await using var verify = CreateContext();
            return await verify.CommitBackfills.SingleAsync(b => b.Id == backfillId);
        }

        //A webhook oldala: csak betétel, és csak bekapcsolt kapcsolóval

        [Fact]
        public async Task WithTheSwitchOff_TheRowIsNotQueued()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await SeedIntegrationAsync(context, seed.Project.Id, autoBackfill: false);
            var (sut, _, queue) = ServiceFactory.CreateGitWebhookServiceWithQueue(context);

            await sut.ProcessPushEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitLab, TruncatedPush());

            //A hiány feljegyzése ettől független: az mindig megtörténik
            Assert.Single(await context.CommitBackfills.ToListAsync());
            Assert.Empty(queue.Enqueued);
        }

        [Fact]
        public async Task WithTheSwitchOn_TheRowIsQueued()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await SeedIntegrationAsync(context, seed.Project.Id, autoBackfill: true);
            var (sut, _, queue) = ServiceFactory.CreateGitWebhookServiceWithQueue(context);

            await sut.ProcessPushEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitLab, TruncatedPush());

            await using var verify = CreateContext();
            var backfill = await verify.CommitBackfills.SingleAsync();

            Assert.Equal(backfill.Id, Assert.Single(queue.Enqueued));

            //A webhook csak betett: a futás a háttérben indul, tehát a sor még érintetlen
            Assert.Equal(BackfillStates.Pending, backfill.State);
            Assert.Equal(0, backfill.AttemptCount);
        }

        /// <summary>
        /// Teli várólistánál semmi nem veszik el: a sor `Pending` marad, és a felhasználó a
        /// felületről elindíthatja. A torlódás tünet, nem hibaág - ezért nem dob kivételt, és
        /// nem is állítja a sort hibásra.
        /// </summary>
        [Fact]
        public async Task AFullQueue_LeavesTheRowForAManualRun()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await SeedIntegrationAsync(context, seed.Project.Id, autoBackfill: true);
            var (sut, _, queue) = ServiceFactory.CreateGitWebhookServiceWithQueue(context);

            queue.IsFull = true;

            await sut.ProcessPushEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitLab, TruncatedPush());

            await using var verify = CreateContext();
            var backfill = await verify.CommitBackfills.SingleAsync();

            Assert.Equal(BackfillStates.Pending, backfill.State);
            Assert.Empty(queue.Enqueued);
        }

        //A futás oldala

        [Fact]
        public async Task TheAutomaticRun_ClaimsWithoutAUserAndLinksTheCommits()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await SeedIntegrationAsync(context, seed.Project.Id, autoBackfill: true);
            var backfill = await SeedBackfillAsync(context, integration.Id);

            var fetcher = new StubGitCommitFetcher
            {
                Outcome = GitFetchOutcome.Ok([Commit("AAA-1 kimaradt commit")])
            };
            var (sut, ctx) = ServiceFactory.CreateGitBackfillService(context, seed.Owner.Id, fetcher);

            await sut.RunAutomaticAsync(backfill.Id);

            var stored = await ReloadAsync(backfill.Id);
            Assert.Equal(BackfillStates.Completed, stored.State);
            Assert.Equal(1, stored.AttemptCount);
            Assert.NotNull(stored.ClaimedAt);

            //Az üres foglaló azonosító jelöli a rendszerindítást - ebből írja ki a felület,
            //hogy a kísérlet magától futott, és a kudarc nem a felhasználó mulasztása
            Assert.Null(stored.ClaimedById);

            var row = Assert.Single(await sut.GetBackfillsAsync(seed.Project.Id));
            Assert.True(row.LastAttemptWasAutomatic);

            await using var verify = CreateContext();
            Assert.Single(await verify.CommitLinks.Where(cl => cl.IntegrationId == integration.Id).ToListAsync());

            //A háttérfutásban nincs bejelentkezett felhasználó, tehát a bejegyzés cselekvő nélküli
            var activity = await verify.Activities.SingleAsync(a => a.Action == "CommitsBackfilled");
            Assert.Null(activity.ActorId);
            Assert.Contains("Automatikus", activity.Description);

            Assert.True(ctx.Hub.SentToProject(seed.Project.Id, "BackfillUpdated"));
        }

        /// <summary>
        /// A kapcsolót a várólistára tétel óta is kikapcsolhatták: a döntés pillanata az
        /// indítás, nem az észlelés. Enélkül egy kikapcsolás után is futna még egy kimenő
        /// hívás - azzal a tokennel, amelyet a felhasználó épp nem akar használni.
        /// </summary>
        [Fact]
        public async Task IfTheSwitchWasTurnedOffMeanwhile_TheRunIsSkipped()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await SeedIntegrationAsync(context, seed.Project.Id, autoBackfill: true);
            var backfill = await SeedBackfillAsync(context, integration.Id);

            integration.AutoBackfillEnabled = false;
            await context.SaveChangesAsync();

            var fetcher = new StubGitCommitFetcher();
            var (sut, _) = ServiceFactory.CreateGitBackfillService(context, seed.Owner.Id, fetcher);

            await sut.RunAutomaticAsync(backfill.Id);

            Assert.Equal(0, fetcher.Calls);

            var stored = await ReloadAsync(backfill.Id);
            Assert.Equal(BackfillStates.Pending, stored.State);
            Assert.Equal(0, stored.AttemptCount);
        }

        /// <summary>
        /// „Pontosan egyszer": a foglalás érintetlen sort kíván. Ez nem a hívási hely
        /// véletlenjén múlik, hanem kikényszerített - egy újraküldés vagy egy közben lefutott
        /// kézi indítás után az automatika nem fut újra, tehát nem éget kvótát.
        /// </summary>
        [Fact]
        public async Task ARowThatAlreadyHasAnAttempt_IsNotRunAutomatically()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await SeedIntegrationAsync(context, seed.Project.Id, autoBackfill: true);
            var backfill = await SeedBackfillAsync(
                context, integration.Id, state: BackfillStates.Failed, attemptCount: 1);

            var fetcher = new StubGitCommitFetcher();
            var (sut, _) = ServiceFactory.CreateGitBackfillService(context, seed.Owner.Id, fetcher);

            await sut.RunAutomaticAsync(backfill.Id);

            Assert.Equal(0, fetcher.Calls);
            Assert.Equal(BackfillStates.Failed, (await ReloadAsync(backfill.Id)).State);
        }

        [Fact]
        public async Task AFailedAutomaticRun_IsNotRetriedAutomatically()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await SeedIntegrationAsync(context, seed.Project.Id, autoBackfill: true);
            var backfill = await SeedBackfillAsync(context, integration.Id);

            var fetcher = new StubGitCommitFetcher
            {
                Outcome = GitFetchOutcome.Fail(
                    GitFetchErrorKind.Authentication, "A szolgáltató elutasította a hitelesítő adatot.")
            };
            var (sut, _) = ServiceFactory.CreateGitBackfillService(context, seed.Owner.Id, fetcher);

            await sut.RunAutomaticAsync(backfill.Id);

            var stored = await ReloadAsync(backfill.Id);
            Assert.Equal(BackfillStates.Failed, stored.State);
            Assert.Contains("hitelesítő", stored.LastError);
            Assert.Equal(1, stored.AttemptCount);

            //Második automatikus kísérlet nincs: innen a felhasználó veszi át
            await sut.RunAutomaticAsync(backfill.Id);

            Assert.Equal(1, fetcher.Calls);
            Assert.Equal(1, (await ReloadAsync(backfill.Id)).AttemptCount);

            //A kézi indítás viszont működik
            var response = await sut.RunBackfillAsync(seed.Project.Id, backfill.Id);
            Assert.Equal(2, fetcher.Calls);
            Assert.Equal(BackfillStates.Failed, response.State);
        }

        /// <summary>
        /// Kérésszám-korlátnál a sor `Pending` marad, nem `Failed`: nem a szolgáltató
        /// utasított el minket, és a hiány pótolható - csak most nem.
        /// </summary>
        [Fact]
        public async Task WhenTheProjectIsRateLimited_TheRowStaysPending()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await SeedIntegrationAsync(context, seed.Project.Id, autoBackfill: true);
            var backfill = await SeedBackfillAsync(context, integration.Id);

            var fetcher = new StubGitCommitFetcher();
            var (sut, ctx) = ServiceFactory.CreateGitBackfillService(context, seed.Owner.Id, fetcher);

            ctx.RateLimit.Limit($"git-backfill:{seed.Project.Id}");

            await sut.RunAutomaticAsync(backfill.Id);

            Assert.Equal(0, fetcher.Calls);

            var stored = await ReloadAsync(backfill.Id);
            Assert.Equal(BackfillStates.Pending, stored.State);
            Assert.Equal(0, stored.AttemptCount);
        }

        [Fact]
        public async Task ADisabledIntegration_IsNotCalledAutomatically()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await SeedIntegrationAsync(context, seed.Project.Id, autoBackfill: true);
            var backfill = await SeedBackfillAsync(context, integration.Id);

            integration.IsEnabled = false;
            await context.SaveChangesAsync();

            var fetcher = new StubGitCommitFetcher();
            var (sut, _) = ServiceFactory.CreateGitBackfillService(context, seed.Owner.Id, fetcher);

            await sut.RunAutomaticAsync(backfill.Id);

            Assert.Equal(0, fetcher.Calls);
            Assert.Equal(BackfillStates.Pending, (await ReloadAsync(backfill.Id)).State);
        }

        /// <summary>
        /// A háttérfutásnak nincs kinek kivételt dobnia, és egy kezeletlen kivétel a
        /// BackgroundService alapértelmezése szerint a teljes API-példányt leállítaná. Egy
        /// eltűnt sor (például közben törölt integráció) ezért csendes naplósor, nem hiba.
        /// </summary>
        [Fact]
        public async Task AMissingRow_DoesNotThrow()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var (sut, _) = ServiceFactory.CreateGitBackfillService(
                context, seed.Owner.Id, new StubGitCommitFetcher());

            await sut.RunAutomaticAsync(Guid.NewGuid());
        }
    }
}
