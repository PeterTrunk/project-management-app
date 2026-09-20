using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.Model;
using ProjectManager.API.Services.GitWebhookService.Payloads;
using ProjectManager.IntegrationTests.Infrastructure;

namespace ProjectManager.IntegrationTests.GitWebhook
{
    /// <summary>
    /// A kézi átrendelés és a webhook viszonya.
    ///
    /// A forgatókönyv: a felhasználó rossz task kulcsot ír a commit üzenetébe,
    /// ezért az illesztő a helytelen taskhoz kapcsolja, a felhasználó kézzel módosítja a hozzárendelést.
    /// Majd egy újabb esemény (forcepush, szerkesztés, merge) újra lefut ugyanarra a commitra / PR-re.
    /// Védelem nélkül az illesztő ismét megtalálná az eredeti kulcsot, nem találna hozzá létező sort (hiszen az már máshová mutat),
    /// és létrehozna egy újat, a módosítés ettől gyakorlatilag visszafordulna, az elem pedig mindkettő task alatt megjelenne.
    /// </summary>
    public class ManualLinkTests : DatabaseTestBase
    {
        public ManualLinkTests(PostgresFixture fixture) : base(fixture) { }

        private const string Sha = "aabbccddeeff00112233445566778899aabbccdd";
        private const int PrNumber = 42;

        private static GitPushEvent Push(string message) =>
            new([new GitCommitInfo(
                Sha: Sha,
                Message: message,
                Url: "https://example.com/commit/aabbccdd",
                AuthorName: "Teszt Elek",
                AuthorEmail: "teszt@example.com",
                CommittedAt: new DateTime(2026, 9, 19, 12, 0, 0, DateTimeKind.Utc))], 0);

        private static GitPullRequestEvent PrEvent(
            string title,
            string state = GitPrStates.Open,
            GitPullRequestAction action = GitPullRequestAction.Opened) =>
            new(
                Number: PrNumber,
                Title: title,
                Description: null,
                Url: "https://example.com/pr/42",
                AuthorName: "Teszt Elek",
                Action: action,
                State: state,
                MergedAt: null);

        //A jelölő beállítása

        [Fact]
        public async Task AssigningACommit_MarksItAsManual()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var second = await TestData.AddTaskAsync(context, seed, "AAA-2");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);

            var (webhook, _) = ServiceFactory.CreateGitWebhookService(context);
            await webhook.ProcessPushEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub, Push("AAA-1 javítás"));

            var link = await SingleCommitLinkAsync(integration.Id);
            Assert.False(link.IsManuallyLinked);

            var (git, _) = ServiceFactory.CreateGitService(context, seed.Owner.Id);
            await git.AssignCommitToTaskAsync(seed.Project.Id, link.Id, second.Id);

            var reassigned = await SingleCommitLinkAsync(integration.Id);
            Assert.Equal(second.Id, reassigned.TaskId);
            Assert.True(reassigned.IsManuallyLinked);
        }

        [Fact]
        public async Task AssigningAPullRequest_MarksItAsManual()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var second = await TestData.AddTaskAsync(context, seed, "AAA-2");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);

            var (webhook, _) = ServiceFactory.CreateGitWebhookService(context);
            await webhook.ProcessPullRequestEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub, PrEvent("AAA-1 javítás"));

            var link = await SinglePrLinkAsync(integration.Id);
            Assert.False(link.IsManuallyLinked);

            var (git, _) = ServiceFactory.CreateGitService(context, seed.Owner.Id);
            await git.AssignPrToTaskAsync(seed.Project.Id, link.Id, second.Id);

            var reassigned = await SinglePrLinkAsync(integration.Id);
            Assert.Equal(second.Id, reassigned.TaskId);
            Assert.True(reassigned.IsManuallyLinked);
        }

        //A védelem: a webhook nem bírálja felül az embert
        /// <summary>
        /// Forcepush ugyanazzal az üzenettel, ami még az eredeti, hibásan beírt kulcsot tartalmazza.
        /// A commit nem kerülhet vissza a régi taskhoz, és nem is jelenhet meg mindkettőnél.
        /// </summary>
        [Fact]
        public async Task ForcePushAfterReassignment_DoesNotUndoTheCorrection()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var second = await TestData.AddTaskAsync(context, seed, "AAA-2");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);

            var (webhook, _) = ServiceFactory.CreateGitWebhookService(context);
            var (git, _) = ServiceFactory.CreateGitService(context, seed.Owner.Id);

            //Rossz kulccsal érkezik, az illesztő az AAA-1-hez kapcsolja
            await webhook.ProcessPushEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub, Push("AAA-1 javítás"));

            //A felhasználó átteszi az AAA-2-re
            var link = await SingleCommitLinkAsync(integration.Id);
            await git.AssignCommitToTaskAsync(seed.Project.Id, link.Id, second.Id);

            //Forcepush: UGYANAZ az üzenet, benne a hibás kulccsal
            await webhook.ProcessPushEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub, Push("AAA-1 javítás"));

            var links = await CommitLinksAsync(integration.Id);

            //Egyetlen sor, és az az EMBER döntése szerinti taskon
            Assert.Equal(second.Id, Assert.Single(links).TaskId);
        }

        /// <summary>
        /// Ugyanez pull requesttel.
        /// Itt gyakoribb a probléma, mert a webhook szerkesztéskor, lezáráskor és mergeléskor is újra lefut.
        /// </summary>
        [Fact]
        public async Task EditingAfterReassignment_DoesNotUndoTheCorrection()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var second = await TestData.AddTaskAsync(context, seed, "AAA-2");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);

            var (webhook, _) = ServiceFactory.CreateGitWebhookService(context);
            var (git, _) = ServiceFactory.CreateGitService(context, seed.Owner.Id);

            await webhook.ProcessPullRequestEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub, PrEvent("AAA-1 javítás"));

            var link = await SinglePrLinkAsync(integration.Id);
            await git.AssignPrToTaskAsync(seed.Project.Id, link.Id, second.Id);

            await webhook.ProcessPullRequestEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub,
                PrEvent("AAA-1 javítás, pontosított cím", action: GitPullRequestAction.Edited));

            var links = await PrLinksAsync(integration.Id);

            Assert.Equal(second.Id, Assert.Single(links).TaskId);
        }

        /// <summary>
        /// A jelölő az illesztést tiltja, nem a frissítést. A szolgáltató adatai: állapot, cím, üzenet - továbbra is átjönnek, 
        /// a hozzákapcsolt task marad a menuálisan hozzárendelt, különben egy módosított hozzárendelésű PR örökre nyitva maradhatna a felületen.
        /// </summary>
        [Fact]
        public async Task ManualLink_StillReceivesStateUpdates()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var second = await TestData.AddTaskAsync(context, seed, "AAA-2");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);

            var (webhook, _) = ServiceFactory.CreateGitWebhookService(context);
            var (git, _) = ServiceFactory.CreateGitService(context, seed.Owner.Id);

            await webhook.ProcessPullRequestEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub, PrEvent("AAA-1 javítás"));

            var link = await SinglePrLinkAsync(integration.Id);
            await git.AssignPrToTaskAsync(seed.Project.Id, link.Id, second.Id);

            await webhook.ProcessPullRequestEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub,
                PrEvent("AAA-1 javítás",
                    state: GitPrStates.Merged,
                    action: GitPullRequestAction.Closed));

            var updated = await SinglePrLinkAsync(integration.Id);

            Assert.Equal(GitPrStates.Merged, updated.State);
            //És a hozzárendelés érintetlen
            Assert.Equal(second.Id, updated.TaskId);
            Assert.True(updated.IsManuallyLinked);
        }

        /// <summary>
        /// A commit üzenete a forcepush során megváltozhat:
        /// azt követni kell, akkor is, ha a hozzárendelés kézi.
        /// </summary>
        [Fact]
        public async Task ManualLink_StillReceivesMessageUpdates()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var second = await TestData.AddTaskAsync(context, seed, "AAA-2");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);

            var (webhook, _) = ServiceFactory.CreateGitWebhookService(context);
            var (git, _) = ServiceFactory.CreateGitService(context, seed.Owner.Id);

            await webhook.ProcessPushEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub, Push("AAA-1 javítás"));

            var link = await SingleCommitLinkAsync(integration.Id);
            await git.AssignCommitToTaskAsync(seed.Project.Id, link.Id, second.Id);

            await webhook.ProcessPushEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub, Push("AAA-1 javítás, pontosítva"));

            var updated = await SingleCommitLinkAsync(integration.Id);

            Assert.Equal("AAA-1 javítás, pontosítva", updated.Message);
            Assert.Equal(second.Id, updated.TaskId);
        }

        /// <summary>
        /// Az illesztő semmilyen új sort nem vesz fel a kézzel rendezett elemhez - 
        /// akkor sem, ha az üzenetbe időközben egy harmadik task kulcsa került.
        /// </summary>
        [Fact]
        public async Task ManualLink_BlocksMatchingEvenForANewKey()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var second = await TestData.AddTaskAsync(context, seed, "AAA-2");
            await TestData.AddTaskAsync(context, seed, "AAA-3");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);

            var (webhook, _) = ServiceFactory.CreateGitWebhookService(context);
            var (git, _) = ServiceFactory.CreateGitService(context, seed.Owner.Id);

            await webhook.ProcessPushEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub, Push("AAA-1 javítás"));

            var link = await SingleCommitLinkAsync(integration.Id);
            await git.AssignCommitToTaskAsync(seed.Project.Id, link.Id, second.Id);

            await webhook.ProcessPushEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub, Push("AAA-1 és AAA-3 javítás"));

            Assert.Single(await CommitLinksAsync(integration.Id));
        }

        /// <summary>
        /// A jelölő nélküli, illesztőtől származó sorokon az újraillesztés TOVÁBBRA IS működik -
        /// a védelem csak a kézi döntésekre szól, nem kapcsolja ki az egész funkciót.
        /// (Ez a 2. etap viselkedése, itt csak őrizzük.)
        /// </summary>
        [Fact]
        public async Task AutomaticLink_IsStillRematchedOnEdit()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var second = await TestData.AddTaskAsync(context, seed, "AAA-2");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);

            var (webhook, _) = ServiceFactory.CreateGitWebhookService(context);

            await webhook.ProcessPullRequestEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub, PrEvent("AAA-1 javítás"));

            await webhook.ProcessPullRequestEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub,
                PrEvent("AAA-1 és AAA-2 javítás", action: GitPullRequestAction.Edited));

            var links = await PrLinksAsync(integration.Id);

            Assert.Equal(2, links.Count);
            Assert.Contains(links, l => l.TaskId == second.Id);
        }

        //Segédek - mindig friss contextből, hogy a change tracker ne hazudhasson
        private async Task<List<CommitLink>> CommitLinksAsync(Guid integrationId)
        {
            await using var verify = CreateContext();
            return await verify.CommitLinks.Where(cl => cl.IntegrationId == integrationId).ToListAsync();
        }

        private async Task<List<PrLink>> PrLinksAsync(Guid integrationId)
        {
            await using var verify = CreateContext();
            return await verify.PrLinks.Where(pl => pl.IntegrationId == integrationId).ToListAsync();
        }

        private async Task<CommitLink> SingleCommitLinkAsync(Guid integrationId) =>
            Assert.Single(await CommitLinksAsync(integrationId));

        private async Task<PrLink> SinglePrLinkAsync(Guid integrationId) =>
            Assert.Single(await PrLinksAsync(integrationId));
    }
}
