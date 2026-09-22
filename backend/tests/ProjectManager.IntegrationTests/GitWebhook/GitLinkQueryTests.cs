using ProjectManager.API.Common.Constants;
using ProjectManager.API.Data;
using ProjectManager.API.Model;
using ProjectManager.API.Services.GitWebhookService.Payloads;
using ProjectManager.IntegrationTests.Infrastructure;

namespace ProjectManager.IntegrationTests.GitWebhook
{
    /// <summary>
    /// A projekt szintű hivatkozás-lista.
    ///
    /// Ez a lekérdezés váltotta le a két korábbi „unmatched" végpontot. 
    /// Azok a hozzárendeletlen sorokra szűrtek - ami az adatbázisban egyetlen feltétel 
    /// viszont a MÁR kapcsolt hivatkozásokhoz egyáltalán nem volt út a felületen. 
    /// Egy rossz task kulccsal beillesztett commit így elérhetetlen volt: a git nézet nem mutatta, 
    /// a task részletnézete pedig csak akkor segít, ha már tudod, melyik taskra ment.
    /// </summary>
    public class GitLinkQueryTests : DatabaseTestBase
    {
        public GitLinkQueryTests(PostgresFixture fixture) : base(fixture) { }

        private static GitPushEvent Push(string message, string sha) =>
            new([new GitCommitInfo(
                Sha: sha,
                Message: message,
                Url: "https://example.com/commit/" + sha[..6],
                AuthorName: "Teszt Elek",
                AuthorEmail: "teszt@example.com",
                CommittedAt: new DateTime(2026, 9, 22, 12, 0, 0, DateTimeKind.Utc))], 0);

        private static GitPullRequestEvent PrEvent(int number, string title) =>
            new(
                Number: number,
                Title: title,
                Description: null,
                Url: $"https://example.com/pr/{number}",
                AuthorName: "Teszt Elek",
                Action: GitPullRequestAction.Opened,
                State: GitPrStates.Open,
                MergedAt: null);

        private const string Sha1 = "aabbccddeeff00112233445566778899aabbccdd";
        private const string Sha2 = "1122334455667788990011223344556677889900";

        //Hozzárendeletlen és kapcsolt egy listában

        [Fact]
        public async Task UnmatchedLink_ComesBackWithoutTaskFields()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (webhook, _) = ServiceFactory.CreateGitWebhookService(context);
            var (git, _) = ServiceFactory.CreateGitService(context, seed.Owner.Id);

            await webhook.ProcessPushEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub, Push("Kulcs nélküli", Sha1));

            var link = Assert.Single(await git.GetCommitLinksAsync(seed.Project.Id));

            Assert.Null(link.TaskId);
            Assert.Null(link.TaskKey);
            Assert.Null(link.TaskTitle);
            Assert.Equal(Sha1, link.CommitSha);
        }

        [Fact]
        public async Task LinkedCommit_CarriesTheTaskKeyAndTitle()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (webhook, _) = ServiceFactory.CreateGitWebhookService(context);
            var (git, _) = ServiceFactory.CreateGitService(context, seed.Owner.Id);

            await webhook.ProcessPushEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub, Push("AAA-1 javítás", Sha1));

            var link = Assert.Single(await git.GetCommitLinksAsync(seed.Project.Id));

            Assert.Equal(seed.Task.Id, link.TaskId);
            Assert.Equal("AAA-1", link.TaskKey);
            Assert.Equal(seed.Task.Title, link.TaskTitle);
        }

        /// <summary>
        /// A kétféle sor EGY listában érkezik - a hívónak nem kell két lekérés a két nézethez.
        /// </summary>
        [Fact]
        public async Task BothKindsComeBackInOneList()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (webhook, _) = ServiceFactory.CreateGitWebhookService(context);
            var (git, _) = ServiceFactory.CreateGitService(context, seed.Owner.Id);

            await webhook.ProcessPushEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub, Push("AAA-1 javítás", Sha1));
            await webhook.ProcessPushEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub, Push("Kulcs nélküli", Sha2));

            var links = await git.GetCommitLinksAsync(seed.Project.Id);

            Assert.Equal(2, links.Count);
            Assert.Single(links, l => l.TaskId == null);
            Assert.Single(links, l => l.TaskId == seed.Task.Id);
        }

        /// <summary>
        /// A lényegi állítás. 
        /// A korábbi tervváltozat a task store-ból akarta építeni ezt a listát,
        /// a store viszont csak a backlog és a nyitott sprintek taskjait tartalmazza
        /// (`TaskService.GetTasksAsync`, `scope == "initial"`). 
        /// Egy lezárt sprintben lévő task hivatkozása így kimaradt volna
        /// pedig épp a régebbi munkákhoz tartozik a legtöbb commit és PR.
        /// 
        /// Ez a lekérdezés nem tud sprintről, tehát nincs mit elrontani rajta.
        /// </summary>
        [Fact]
        public async Task LinkOfATaskInACompletedSprint_IsStillReturned()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (webhook, _) = ServiceFactory.CreateGitWebhookService(context);
            var (git, _) = ServiceFactory.CreateGitService(context, seed.Owner.Id);

            await webhook.ProcessPushEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub, Push("AAA-1 javítás", Sha1));

            //A task bekerül a sprintbe, a sprintet pedig lezárjuk
            await using (var move = CreateContext())
            {
                var task = await move.ProjectTasks.FindAsync(seed.Task.Id);
                var sprint = await move.Sprints.FindAsync(seed.Sprint.Id);
                task!.SprintId = sprint!.Id;
                sprint.State = SprintStates.Completed;
                await move.SaveChangesAsync();
            }

            var link = Assert.Single(await git.GetCommitLinksAsync(seed.Project.Id));

            Assert.Equal(seed.Task.Id, link.TaskId);
            Assert.Equal("AAA-1", link.TaskKey);
        }

        //Projekt-hatókör

        [Fact]
        public async Task LinksOfAnotherProject_DoNotLeak()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedTwoProjectsAsync(context);
            var integrationA = await TestData.SeedIntegrationAsync(context, seed.A.Project.Id);
            var integrationB = await TestData.SeedIntegrationAsync(context, seed.B.Project.Id);
            var (webhook, _) = ServiceFactory.CreateGitWebhookService(context);
            var (git, _) = ServiceFactory.CreateGitService(context, seed.A.Owner.Id);

            await webhook.ProcessPushEventAsync(
                seed.A.Project.Id, integrationA.Id, GitProviders.GitHub, Push("AAA-1 javítás", Sha1));
            await webhook.ProcessPushEventAsync(
                seed.B.Project.Id, integrationB.Id, GitProviders.GitHub, Push("BBB-1 javítás", Sha2));

            var links = await git.GetCommitLinksAsync(seed.A.Project.Id);

            var link = Assert.Single(links);
            Assert.Equal(seed.A.Task.Id, link.TaskId);
            Assert.DoesNotContain(links, l => l.CommitSha == Sha2);
        }

        //A kézi jelölő is utazik - a felület ebből tudja jelezni, mihez nyúlt már ember

        [Fact]
        public async Task ManualFlag_IsCarriedOnTheLink()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var second = await TestData.AddTaskAsync(context, seed, "AAA-2");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (webhook, _) = ServiceFactory.CreateGitWebhookService(context);
            var (git, _) = ServiceFactory.CreateGitService(context, seed.Owner.Id);

            await webhook.ProcessPushEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub, Push("AAA-1 javítás", Sha1));

            Assert.False(Assert.Single(await git.GetCommitLinksAsync(seed.Project.Id)).IsManuallyLinked);

            var linkId = Assert.Single(await git.GetCommitLinksAsync(seed.Project.Id)).Id;
            await git.AssignCommitToTaskAsync(seed.Project.Id, linkId, second.Id);

            var reassigned = Assert.Single(await git.GetCommitLinksAsync(seed.Project.Id));
            Assert.True(reassigned.IsManuallyLinked);
            Assert.Equal("AAA-2", reassigned.TaskKey);
        }

        //Pull requestek - ugyanaz a szerkezet

        [Fact]
        public async Task PrLinks_BehaveTheSameWay()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (webhook, _) = ServiceFactory.CreateGitWebhookService(context);
            var (git, _) = ServiceFactory.CreateGitService(context, seed.Owner.Id);

            await webhook.ProcessPullRequestEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub, PrEvent(1, "AAA-1 javítás"));
            await webhook.ProcessPullRequestEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub, PrEvent(2, "Kulcs nélküli"));

            var links = await git.GetPrLinksAsync(seed.Project.Id);

            Assert.Equal(2, links.Count);

            var matched = Assert.Single(links, l => l.PrNumber == 1);
            Assert.Equal(seed.Task.Id, matched.TaskId);
            Assert.Equal("AAA-1", matched.TaskKey);

            var unmatched = Assert.Single(links, l => l.PrNumber == 2);
            Assert.Null(unmatched.TaskId);
            Assert.Null(unmatched.TaskKey);
        }

        /// <summary>
        /// A legfrissebb elöl: a felületen a listát olvasni kell, nem böngészni.
        /// </summary>
        [Fact]
        public async Task Links_AreOrderedNewestFirst()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (webhook, _) = ServiceFactory.CreateGitWebhookService(context);
            var (git, _) = ServiceFactory.CreateGitService(context, seed.Owner.Id);

            var older = new GitPushEvent([new GitCommitInfo(
                Sha1, "AAA-1 régebbi", null, "Teszt Elek", "teszt@example.com",
                new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc))], 0);

            var newer = new GitPushEvent([new GitCommitInfo(
                Sha2, "AAA-1 újabb", null, "Teszt Elek", "teszt@example.com",
                new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc))], 0);

            await webhook.ProcessPushEventAsync(seed.Project.Id, integration.Id, GitProviders.GitHub, older);
            await webhook.ProcessPushEventAsync(seed.Project.Id, integration.Id, GitProviders.GitHub, newer);

            var links = await git.GetCommitLinksAsync(seed.Project.Id);

            Assert.Equal(Sha2, links[0].CommitSha);
            Assert.Equal(Sha1, links[1].CommitSha);
        }

        [Fact]
        public async Task ProjectWithoutLinks_ReturnsEmptyLists()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var (git, _) = ServiceFactory.CreateGitService(context, seed.Owner.Id);

            Assert.Empty(await git.GetCommitLinksAsync(seed.Project.Id));
            Assert.Empty(await git.GetPrLinksAsync(seed.Project.Id));
        }
    }
}
