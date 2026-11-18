using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.Model;
using ProjectManager.API.Services.GitWebhookService.Payloads;
using ProjectManager.IntegrationTests.Infrastructure;

namespace ProjectManager.IntegrationTests.GitWebhook
{
    /// <summary>
    /// A hiányosan érkezett push nyilvántartásba vétele.
    ///
    /// A szolgáltatók levágják a payload commit tömbjét (GitLab 20, GitHub 2048 commitnál).
    /// Enélkül a sor nélkül ez néma adatvesztés: ha a task kulcsa egy kimaradt commit
    /// üzenetében van, a kapcsolat soha nem jön létre, és semmi nem jelzi.
    ///
    /// Ez az etap MÉG NEM pótol semmit: csak feljegyzi a hiányt. A pótlás külön kérésben indul,
    /// mert a szolgáltató API-jának megkérdezése a webhook válaszidejét kötné a szolgáltatóéhoz.
    /// </summary>
    public class CommitBackfillRecordingTests : DatabaseTestBase
    {
        public CommitBackfillRecordingTests(PostgresFixture fixture) : base(fixture) { }

        private const string Before = "1111111111111111111111111111111111111111";
        private const string After = "2222222222222222222222222222222222222222";

        private static GitCommitInfo Commit(string message, string sha) =>
            new(Sha: sha,
                Message: message,
                Url: "https://example.com/commit/" + sha[..7],
                AuthorName: "Teszt Elek",
                AuthorEmail: "teszt@example.com",
                CommittedAt: new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc));

        private static GitPushEvent TruncatedPush(
            int reported = 25,
            string? afterSha = After,
            string message = "AAA-1 javítás",
            string sha = "aabbccddeeff00112233445566778899aabbccdd") =>
            new([Commit(message, sha)],
                SkippedCommitCount: 0,
                ProviderReportedCommitCount: reported,
                CommitsPossiblyTruncated: true,
                BeforeSha: Before,
                AfterSha: afterSha,
                Ref: "refs/heads/main");

        private static GitPushEvent CompletePush() =>
            new([Commit("AAA-1 javítás", "bbccddeeff00112233445566778899aabbccddee")],
                SkippedCommitCount: 0,
                ProviderReportedCommitCount: 1,
                CommitsPossiblyTruncated: false,
                BeforeSha: Before,
                AfterSha: After,
                Ref: "refs/heads/main");

        private async Task<List<CommitBackfill>> BackfillsAsync(Guid integrationId)
        {
            await using var verify = CreateContext();
            return await verify.CommitBackfills
                .Where(b => b.IntegrationId == integrationId)
                .ToListAsync();
        }

        [Fact]
        public async Task TruncatedPush_IsRecordedAsPending()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (sut, _) = ServiceFactory.CreateGitWebhookService(context);

            await sut.ProcessPushEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitLab, TruncatedPush());

            var backfill = Assert.Single(await BackfillsAsync(integration.Id));

            Assert.Equal(BackfillStates.Pending, backfill.State);
            Assert.Equal(25, backfill.ReportedCommitCount);
            Assert.Equal(1, backfill.ReceivedCommitCount);
            Assert.Equal(Before, backfill.BeforeSha);
            Assert.Equal(After, backfill.AfterSha);
            Assert.Equal("refs/heads/main", backfill.Ref);

            //Senki nem foglalta le, és még egyszer sem próbálkoztunk
            Assert.Null(backfill.ClaimedAt);
            Assert.Null(backfill.ClaimedById);
            Assert.Equal(0, backfill.AttemptCount);
            Assert.Null(backfill.CompletedAt);
        }

        [Fact]
        public async Task CompletePush_IsNotRecorded()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (sut, _) = ServiceFactory.CreateGitWebhookService(context);

            await sut.ProcessPushEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub, CompletePush());

            Assert.Empty(await BackfillsAsync(integration.Id));
        }

        /// <summary>
        /// A szolgáltatók ugyanazt a pusht többször is kézbesíthetik (újraküldés), és abból
        /// nem lehet két pótlandó sor: a felhasználó két gombot látna ugyanarra a hiányra.
        /// </summary>
        [Fact]
        public async Task SameTruncatedPushTwice_IsRecordedOnce()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (sut, _) = ServiceFactory.CreateGitWebhookService(context);

            await sut.ProcessPushEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitLab, TruncatedPush());
            await sut.ProcessPushEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitLab, TruncatedPush());

            Assert.Single(await BackfillsAsync(integration.Id));
        }

        /// <summary>
        /// Azonosító nélkül két hiányos push nem feltétlenül ugyanaz, tehát mindkettőt fel kell
        /// jegyezni. Pótolni nem lehet őket (nincs mihez képest lekérdezni), de a hiány tényét
        /// ettől még ki kell mondani - ez a különbség a néma és a látható adatvesztés között.
        /// </summary>
        [Fact]
        public async Task TruncatedPushesWithoutIdentifiers_AreRecordedSeparately()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (sut, _) = ServiceFactory.CreateGitWebhookService(context);

            await sut.ProcessPushEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitLab,
                TruncatedPush(afterSha: null, sha: "aabbccddeeff00112233445566778899aabbccdd"));

            await sut.ProcessPushEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitLab,
                TruncatedPush(afterSha: null, sha: "ccddeeff00112233445566778899aabbccddeeff"));

            var backfills = await BackfillsAsync(integration.Id);

            Assert.Equal(2, backfills.Count);
            Assert.All(backfills, b => Assert.Null(b.AfterSha));
        }

        /// <summary>
        /// A megérkezett commitok feldolgozása az elsődleges dolga a webhooknak, a feljegyzés
        /// csak ezután következik: a nyilvántartás soha nem viheti magával a kapcsolatokat.
        /// </summary>
        [Fact]
        public async Task TruncatedPush_StillLinksTheCommitsThatArrived()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (sut, _) = ServiceFactory.CreateGitWebhookService(context);

            await sut.ProcessPushEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitLab, TruncatedPush());

            await using var verify = CreateContext();
            var link = Assert.Single(await verify.CommitLinks
                .Where(cl => cl.IntegrationId == integration.Id)
                .ToListAsync());

            Assert.Equal(seed.Task.Id, link.TaskId);
            Assert.Single(await BackfillsAsync(integration.Id));
        }

        /// <summary>
        /// A GitHub nem közöl darabszámot, ott a tömb felső határa a jelzés. A sor ilyenkor is
        /// elkészül, csak az ígért darabszám marad ismeretlen - a hiány mértéke nem tudható,
        /// a ténye igen.
        /// </summary>
        [Fact]
        public async Task TruncatedPushWithoutAReportedCount_IsStillRecorded()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (sut, _) = ServiceFactory.CreateGitWebhookService(context);

            await sut.ProcessPushEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub,
                TruncatedPush(reported: 0) with { ProviderReportedCommitCount = null });

            var backfill = Assert.Single(await BackfillsAsync(integration.Id));

            Assert.Null(backfill.ReportedCommitCount);
            Assert.Equal(1, backfill.ReceivedCommitCount);
            Assert.Equal(BackfillStates.Pending, backfill.State);
        }
    }
}
