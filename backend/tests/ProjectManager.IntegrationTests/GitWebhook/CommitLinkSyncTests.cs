using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.Model;
using ProjectManager.API.Services.GitWebhookService.Payloads;
using ProjectManager.IntegrationTests.Infrastructure;

namespace ProjectManager.IntegrationTests.GitWebhook
{
    /// <summary>
    /// A commit hivatkozások életciklusa. Ugyanaz a szerkezet, mint a pull requesteknél:
    /// egy commit üzenete több task kulcsát is tartalmazhatja, tehát több sora lehet -
    /// és a hibák pont abból fakadtak, hogy a séma és a kód ebben nem értett egyet.
    /// </summary>
    public class CommitLinkSyncTests : DatabaseTestBase
    {
        public CommitLinkSyncTests(PostgresFixture fixture) : base(fixture) { }

        private const string Sha = "aabbccddeeff00112233445566778899aabbccdd";

        private static GitPushEvent Push(string message, string sha = Sha) =>
            new([new GitCommitInfo(
                Sha: sha,
                Message: message,
                Url: "https://example.com/commit/aabbccdd",
                AuthorName: "Teszt Elek",
                AuthorEmail: "teszt@example.com",
                CommittedAt: new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc))], 0);

        private async Task<List<CommitLink>> CommitLinksAsync(Guid integrationId)
        {
            await using var verify = CreateContext();
            return await verify.CommitLinks
                .Where(cl => cl.IntegrationId == integrationId)
                .ToListAsync();
        }

        /// <summary>
        /// A legsúlyosabb következmény: a két sor beszúrása egyedi index sértést adott,
        /// vagyis egy két taskot említő commit üzenettől a webhook 500-zal szállt el.
        /// </summary>
        [Fact]
        public async Task CommitMentioningTwoTasks_LinksBoth()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var second = await TestData.AddTaskAsync(context, seed, "AAA-2");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (sut, _) = ServiceFactory.CreateGitWebhookService(context);

            await sut.ProcessPushEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub,
                Push("AAA-1 és AAA-2 együtt javítva"));

            var links = await CommitLinksAsync(integration.Id);

            Assert.Equal(2, links.Count);
            Assert.Equal(
                new[] { seed.Task.Id, second.Id }.OrderBy(id => id),
                links.Select(l => l.TaskId!.Value).OrderBy(id => id));
        }

        [Fact]
        public async Task CommitWithoutAKey_IsRecordedAsUnmatched()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (sut, _) = ServiceFactory.CreateGitWebhookService(context);

            await sut.ProcessPushEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub, Push("Kulcs nélküli commit"));

            Assert.Null(Assert.Single(await CommitLinksAsync(integration.Id)).TaskId);
        }

        [Fact]
        public async Task SamePushTwice_DoesNotDuplicateTheLink()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (sut, _) = ServiceFactory.CreateGitWebhookService(context);

            await sut.ProcessPushEventAsync(seed.Project.Id, integration.Id, GitProviders.GitHub, Push("AAA-1 javítás"));
            await sut.ProcessPushEventAsync(seed.Project.Id, integration.Id, GitProviders.GitHub, Push("AAA-1 javítás"));

            Assert.Single(await CommitLinksAsync(integration.Id));
        }

        /// <summary>
        /// Forcepush ugyanarra a sha-ra, javított üzenettel: az üzenet frissül, új sor nem keletkezik.
        /// </summary>
        [Fact]
        public async Task ForcePushWithAnAmendedMessage_UpdatesTheExistingLink()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (sut, _) = ServiceFactory.CreateGitWebhookService(context);

            await sut.ProcessPushEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub, Push("AAA-1 javítás"));

            await sut.ProcessPushEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub, Push("AAA-1 javítás, pontosítva"));

            var link = Assert.Single(await CommitLinksAsync(integration.Id));
            Assert.Equal("AAA-1 javítás, pontosítva", link.Message);
        }

        /// <summary>
        /// Egy hozzárendeletlen commit forcepush után már tartalmazza a kulcsot. A hozzárendelés
        /// létrejön, és a helyőrző sor eltűnik - különben a commit egyszerre látszana a
        /// "hozzárendeletlen" listában és a task alatt.
        /// </summary>
        [Fact]
        public async Task ForcePushAddingAKey_ReplacesTheUnmatchedPlaceholder()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (sut, _) = ServiceFactory.CreateGitWebhookService(context);

            await sut.ProcessPushEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub, Push("Elfelejtett kulcs"));

            Assert.Null(Assert.Single(await CommitLinksAsync(integration.Id)).TaskId);

            await sut.ProcessPushEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub, Push("AAA-1 elfelejtett kulcs"));

            var link = Assert.Single(await CommitLinksAsync(integration.Id));
            Assert.Equal(seed.Task.Id, link.TaskId);
        }

        [Fact]
        public async Task TwoDifferentCommits_GetTheirOwnLinks()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (sut, _) = ServiceFactory.CreateGitWebhookService(context);

            await sut.ProcessPushEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub, Push("AAA-1 első"));
            await sut.ProcessPushEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub,
                Push("AAA-1 második", sha: "1122334455667788990011223344556677889900"));

            Assert.Equal(2, (await CommitLinksAsync(integration.Id)).Count);
        }

        [Fact]
        public async Task LinkedCommit_IsBroadcastAndLogged()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id, GitProviders.GitLab);
            var (sut, ctx) = ServiceFactory.CreateGitWebhookService(context);

            await sut.ProcessPushEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitLab, Push("AAA-1 javítás"));

            Assert.True(ctx.Hub.SentToProject(seed.Project.Id, "CommitLinked"));

            await using var verify = CreateContext();
            var activity = await verify.Activities
                .SingleAsync(a => a.ProjectId == seed.Project.Id && a.EntityType == "Commit");

            //A provider neve a payloadból jön, nem beégetve
            Assert.StartsWith("GitLab ", activity.Description);
        }

        /// <summary>
        /// Egy csonka payloadból rövidebb azonosító is érkezhet. A szöveg vágása korábban
        /// itt kivételt dobott volna.
        /// </summary>
        [Fact]
        public async Task ShortCommitSha_DoesNotBreakTheActivityText()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (sut, _) = ServiceFactory.CreateGitWebhookService(context);

            await sut.ProcessPushEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub, Push("AAA-1 javítás", sha: "abc"));

            var link = Assert.Single(await CommitLinksAsync(integration.Id));
            Assert.Equal("abc", link.CommitSha);
        }
    }
}
