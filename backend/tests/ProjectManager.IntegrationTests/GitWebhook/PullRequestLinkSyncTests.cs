using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.Model;
using ProjectManager.API.Services.GitWebhookService.Payloads;
using ProjectManager.IntegrationTests.Infrastructure;

namespace ProjectManager.IntegrationTests.GitWebhook
{
    /// <summary>
    /// A pull request hivatkozások életciklusa: létrejövés, újraillesztés és állapotkövetés.
    ///
    /// Ezek adatbázist igényelnek, mert a mért viselkedés maga a TÖBB SOR kezelése - egy PR
    /// több taskhoz is illeszkedhet, és a hibák pont abból fakadtak, hogy a kód egyetlen sorral
    /// számolt. Egy tiszta függvény tesztje ezt nem tudná megfogni.
    /// </summary>
    public class PullRequestLinkSyncTests : DatabaseTestBase
    {
        public PullRequestLinkSyncTests(PostgresFixture fixture) : base(fixture) { }

        private const int PrNumber = 42;

        private static GitPullRequestEvent PrEvent(
            string title,
            string? description = null,
            string state = GitPrStates.Open,
            GitPullRequestAction action = GitPullRequestAction.Opened,
            DateTime? mergedAt = null) =>
            new(
                Number: PrNumber,
                Title: title,
                Description: description,
                Url: "https://example.com/pr/42",
                AuthorName: "Teszt Elek",
                Action: action,
                State: state,
                MergedAt: mergedAt);

        private async Task<List<PrLink>> PrLinksAsync(Guid integrationId)
        {
            //Friss context: a change tracker ne hazudhassa azt, amit a szolgáltatás
            //csak a memóriában állított be
            await using var verify = CreateContext();
            return await verify.PrLinks
                .Where(pl => pl.IntegrationId == integrationId)
                .OrderBy(pl => pl.CreatedAt)
                .ToListAsync();
        }

        //Illesztés a leírásból

        [Fact]
        public async Task KeyOnlyInTheDescription_LinksTheTask()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (sut, _) = ServiceFactory.CreateGitWebhookService(context);

            //A cím nem tartalmaz kulcsot - korábban ez semmit nem kapcsolt össze
            await sut.ProcessPullRequestEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub,
                PrEvent("Kulcs nélküli cím", "Ez a PR az AAA-1 taskot zárja le."));

            var links = await PrLinksAsync(integration.Id);

            var link = Assert.Single(links);
            Assert.Equal(seed.Task.Id, link.TaskId);
        }

        [Fact]
        public async Task NoKeyAnywhere_CreatesAnUnmatchedLink()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (sut, _) = ServiceFactory.CreateGitWebhookService(context);

            await sut.ProcessPullRequestEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub,
                PrEvent("Semmi kulcs", "A leírásban sincs."));

            var link = Assert.Single(await PrLinksAsync(integration.Id));
            Assert.Null(link.TaskId);
        }

        //Több task, több sor

        [Fact]
        public async Task TwoMatchedTasks_GetOneLinkEach()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var second = await TestData.AddTaskAsync(context, seed, "AAA-2");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (sut, _) = ServiceFactory.CreateGitWebhookService(context);

            await sut.ProcessPullRequestEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub,
                PrEvent("AAA-1 hibajavítás", "Egyúttal az AAA-2 is elkészült."));

            var links = await PrLinksAsync(integration.Id);

            Assert.Equal(2, links.Count);
            Assert.Equal(
                new[] { seed.Task.Id, second.Id }.OrderBy(id => id),
                links.Select(l => l.TaskId!.Value).OrderBy(id => id));
        }

        /// <summary>
        /// A lényegi regresszió. A létező sort korábban FirstOrDefault kereste meg, tehát
        /// merge után csak az ELSŐ kapott "merged" állapotot - a többi task alatt a PR
        /// a végtelenségig "open" maradt.
        /// </summary>
        [Fact]
        public async Task Merge_UpdatesEveryLinkOfThePullRequest()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            await TestData.AddTaskAsync(context, seed, "AAA-2");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (sut, _) = ServiceFactory.CreateGitWebhookService(context);

            await sut.ProcessPullRequestEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub,
                PrEvent("AAA-1 és AAA-2 együtt"));

            Assert.Equal(2, (await PrLinksAsync(integration.Id)).Count);

            var mergedAt = new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);
            await sut.ProcessPullRequestEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub,
                PrEvent("AAA-1 és AAA-2 együtt",
                    state: GitPrStates.Merged,
                    action: GitPullRequestAction.Closed,
                    mergedAt: mergedAt));

            var links = await PrLinksAsync(integration.Id);

            Assert.Equal(2, links.Count);
            Assert.All(links, link =>
            {
                Assert.Equal(GitPrStates.Merged, link.State);
                Assert.Equal(mergedAt, link.MergedAt);
            });
        }

        //Újraillesztés szerkesztésnél

        /// <summary>
        /// A szerkesztés ága korábban korán visszatért: aki utólag írta bele a kulcsot,
        /// annál az összekapcsolás sosem jött létre.
        /// </summary>
        [Fact]
        public async Task EditingInAKey_LinksTheTaskAfterwards()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (sut, _) = ServiceFactory.CreateGitWebhookService(context);

            await sut.ProcessPullRequestEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub,
                PrEvent("Kulcs nélküli cím"));

            Assert.Null(Assert.Single(await PrLinksAsync(integration.Id)).TaskId);

            await sut.ProcessPullRequestEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub,
                PrEvent("AAA-1 hibajavítás", action: GitPullRequestAction.Edited));

            var link = Assert.Single(await PrLinksAsync(integration.Id));

            //A hozzárendeletlen helyőrző eltűnt: különben a PR egyszerre látszana a
            //"hozzárendeletlen" listában és a task alatt
            Assert.Equal(seed.Task.Id, link.TaskId);
        }

        [Fact]
        public async Task EditingInASecondKey_AddsOnlyTheMissingLink()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var second = await TestData.AddTaskAsync(context, seed, "AAA-2");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (sut, _) = ServiceFactory.CreateGitWebhookService(context);

            await sut.ProcessPullRequestEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub, PrEvent("AAA-1 javítás"));

            var firstLinkId = Assert.Single(await PrLinksAsync(integration.Id)).Id;

            await sut.ProcessPullRequestEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub,
                PrEvent("AAA-1 javítás", "Plusz az AAA-2.", action: GitPullRequestAction.Edited));

            var links = await PrLinksAsync(integration.Id);

            Assert.Equal(2, links.Count);
            //A meglévő sor NEM keletkezett újra: az azonosítója ugyanaz maradt
            Assert.Contains(links, l => l.Id == firstLinkId && l.TaskId == seed.Task.Id);
            Assert.Contains(links, l => l.TaskId == second.Id);
        }

        [Fact]
        public async Task RepeatedEventWithTheSameKey_DoesNotDuplicateTheLink()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (sut, _) = ServiceFactory.CreateGitWebhookService(context);

            var prEvent = PrEvent("AAA-1 javítás");

            await sut.ProcessPullRequestEventAsync(seed.Project.Id, integration.Id, GitProviders.GitHub, prEvent);
            await sut.ProcessPullRequestEventAsync(seed.Project.Id, integration.Id, GitProviders.GitHub, prEvent);
            await sut.ProcessPullRequestEventAsync(seed.Project.Id, integration.Id, GitProviders.GitHub, prEvent);

            Assert.Single(await PrLinksAsync(integration.Id));
        }

        [Fact]
        public async Task EditingOutTheKey_KeepsTheExistingLink()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (sut, _) = ServiceFactory.CreateGitWebhookService(context);

            await sut.ProcessPullRequestEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub, PrEvent("AAA-1 javítás"));

            //A kulcs kikerül a címből. Az összekapcsolást NEM bontjuk fel: az már megtörtént
            //tény, és a felhasználó kézzel is állíthatta - a webhook ne törölje a döntését.
            await sut.ProcessPullRequestEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub,
                PrEvent("Már nincs benne kulcs", action: GitPullRequestAction.Edited));

            var link = Assert.Single(await PrLinksAsync(integration.Id));
            Assert.Equal(seed.Task.Id, link.TaskId);
            Assert.Equal("Már nincs benne kulcs", link.Title);
        }

        //Valós idejű értesítés

        /// <summary>
        /// Enélkül a mentés megtörténik, de a böngésző nem tud róla: a felhasználó egy
        /// mergelt PR-t "open" jelöléssel lát egészen az oldal újratöltéséig.
        /// </summary>
        [Fact]
        public async Task StateChange_IsBroadcastToEveryLinkedTask()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            await TestData.AddTaskAsync(context, seed, "AAA-2");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (sut, ctx) = ServiceFactory.CreateGitWebhookService(context);

            await sut.ProcessPullRequestEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub, PrEvent("AAA-1 és AAA-2"));

            ctx.Hub.Clear();

            await sut.ProcessPullRequestEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub,
                PrEvent("AAA-1 és AAA-2",
                    state: GitPrStates.Merged,
                    action: GitPullRequestAction.Closed,
                    mergedAt: DateTime.UtcNow));

            var prLinked = ctx.Hub.CallsToProject(seed.Project.Id)
                .Where(c => c.Method == "PrLinked")
                .ToList();

            //Mindkét task megkapja a friss sort
            Assert.Equal(2, prLinked.Count);
        }

        [Fact]
        public async Task UnchangedState_DoesNotBroadcastAgain()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (sut, ctx) = ServiceFactory.CreateGitWebhookService(context);

            await sut.ProcessPullRequestEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub, PrEvent("AAA-1 javítás"));

            ctx.Hub.Clear();

            //Puszta címátírás: nincs állapotváltozás, tehát nincs mit hirdetni.
            //Enélkül minden szerkesztés bejegyzést szórna a tevékenységlistába.
            await sut.ProcessPullRequestEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub,
                PrEvent("AAA-1 javított cím", action: GitPullRequestAction.Edited));

            Assert.Empty(ctx.Hub.CallsToProject(seed.Project.Id));
        }

        /// <summary>
        /// A provider neve a payloadból jön: korábban "GitHub" volt beégetve a szövegbe,
        /// tehát egy GitLab merge requestről is azt írta volna ki.
        /// </summary>
        [Fact]
        public async Task ActivityText_NamesTheActualProvider()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id, GitProviders.GitLab);
            var (sut, _) = ServiceFactory.CreateGitWebhookService(context);

            await sut.ProcessPullRequestEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitLab, PrEvent("AAA-1 javítás"));

            await using var verify = CreateContext();
            var activity = await verify.Activities
                .SingleAsync(a => a.ProjectId == seed.Project.Id && a.EntityType == "PullRequest");

            Assert.StartsWith("GitLab ", activity.Description);
            Assert.DoesNotContain("GitHub", activity.Description);
        }
    }
}
