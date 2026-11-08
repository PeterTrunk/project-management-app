using ProjectManager.API.Common.Constants;
using ProjectManager.API.Services.GitWebhookService.Payloads;
using ProjectManager.IntegrationTests.Infrastructure;

namespace ProjectManager.IntegrationTests.GitWebhook
{
    /// <summary>
    /// A hozzárendeletlen git hivatkozások kiküldése.
    ///
    /// <b>Miért van ez a tesztosztály.</b> A webhook korábban CSAK akkor szólt a böngészőnek,
    /// ha a hivatkozás illeszkedett egy taskra. A hozzárendeletlen sorok három helyen
    /// keletkeztek vagy változtak némán:
    ///
    /// <list type="number">
    /// <item>hozzárendeletlen commit érkezik - csak tevékenység-bejegyzés készült,</item>
    /// <item>hozzárendeletlen PR érkezik - ugyanaz,</item>
    /// <item>hozzárendeletlen PR állapota változik (mergelik) - teljesen nyomtalan volt, mert
    /// az állapotkövető ciklus <c>TaskId.HasValue</c>-ra szűrt.</item>
    /// </list>
    ///
    /// Ez addig nem látszott, amíg a git nézet minden eseményre MINDKÉT listát újratöltötte:
    /// egy véletlenül beeső illeszkedő commit a hozzárendeletlen listát is felfrissítette.
    /// A tárolóra átállás után viszont a payload az egyetlen forrás, tehát a hiányzó kiküldés
    /// azt jelentené, hogy a hozzárendeletlen lista csak oldalfrissítésre változik.
    ///
    /// <b>Új eseménynév nincs.</b> Ugyanaz a <c>CommitLinked</c> / <c>PrLinked</c> megy ki
    /// <c>taskId: null</c>-lal - a kliens a hivatkozás azonosítója szerint tárol, tehát a
    /// <c>null</c> ott egyszerű mező, nem különleges eset.
    /// </summary>
    public class UnmatchedLinkBroadcastTests : DatabaseTestBase
    {
        public UnmatchedLinkBroadcastTests(PostgresFixture fixture) : base(fixture) { }

        private const int PrNumber = 7;
        private const string Sha = "0011223344556677889900112233445566778899";

        private static GitPushEvent Push(string message) =>
            new([new GitCommitInfo(
                Sha: Sha,
                Message: message,
                Url: "https://example.invalid/commit/00112233",
                AuthorName: "Teszt Elek",
                AuthorEmail: "teszt@example.invalid",
                CommittedAt: new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc))], 0);

        private static GitPullRequestEvent PrEvent(
            string title,
            string state = GitPrStates.Open,
            GitPullRequestAction action = GitPullRequestAction.Opened,
            DateTime? mergedAt = null) =>
            new(
                Number: PrNumber,
                Title: title,
                Description: null,
                Url: "https://example.invalid/pr/7",
                AuthorName: "Teszt Elek",
                Action: action,
                State: state,
                MergedAt: mergedAt);

        /// <summary>
        /// A payload névtelen objektum, tehát reflexióval olvassuk. Szándékosan NEM a
        /// szolgáltatás belső alakját tükrözzük vissza egy segédtípusba: az elrejtené, ha egy
        /// mező neve megváltozik - pedig épp a mezőnév az, amire a kliens épül.
        /// </summary>
        private static object? Field(object payload, string name) =>
            payload.GetType().GetProperty(name)?.GetValue(payload);

        private object SingleCall(RecordingHubContext hub, Guid projectId, string method)
        {
            var call = Assert.Single(hub.CallsToProject(projectId), c => c.Method == method);
            return Assert.IsType<object>(call.Arguments[0], exactMatch: false);
        }

        [Fact]
        public async Task UnmatchedCommit_IsBroadcastWithANullTaskId()
        {
            // EZ A LÉNYEGI ÁLLÍTÁS az 1. hézagra: a kulcs nélküli commit üzenet is esemény.
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (sut, ctx) = ServiceFactory.CreateGitWebhookService(context);

            await sut.ProcessPushEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub,
                Push("Elfelejtettem a task kulcsot"));

            var payload = SingleCall(ctx.Hub, seed.Project.Id, "CommitLinked");

            Assert.Null(Field(payload, "taskId"));
            //A sha azért kell a payloadban, mert a kliens EBBŐL tudja kivenni a
            //hozzárendeletlen sort, ha később ugyanarra a sha-ra illeszkedő sor érkezik.
            Assert.Equal(Sha, Field(payload, "commitSha"));
            Assert.NotNull(Field(payload, "id"));
        }

        [Fact]
        public async Task UnmatchedPullRequest_IsBroadcastWithANullTaskId()
        {
            // A 2. hézag.
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (sut, ctx) = ServiceFactory.CreateGitWebhookService(context);

            await sut.ProcessPullRequestEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub,
                PrEvent("Kulcs nélküli PR"));

            var payload = SingleCall(ctx.Hub, seed.Project.Id, "PrLinked");

            Assert.Null(Field(payload, "taskId"));
            //A PR száma a sha párja: ez azonosítja a hozzárendeletlen sort a kliensen.
            Assert.Equal(PrNumber, Field(payload, "prNumber"));
        }

        [Fact]
        public async Task MergingAnUnmatchedPullRequest_IsBroadcast()
        {
            // EZ A LEGSÚLYOSABB a három közül (3. hézag): a korábbi `TaskId.HasValue` szűrő
            // miatt egy hozzárendeletlen PR mergelése TELJESEN nyomtalan volt - a git nézet
            // "open" jelöléssel mutatta tovább, akár a munkamenet végéig.
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (sut, ctx) = ServiceFactory.CreateGitWebhookService(context);

            await sut.ProcessPullRequestEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub, PrEvent("Kulcs nélküli PR"));

            ctx.Hub.Clear();

            await sut.ProcessPullRequestEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub,
                PrEvent("Kulcs nélküli PR",
                    state: GitPrStates.Merged,
                    action: GitPullRequestAction.Closed,
                    mergedAt: new DateTime(2026, 11, 8, 13, 0, 0, DateTimeKind.Utc)));

            var payload = SingleCall(ctx.Hub, seed.Project.Id, "PrLinked");

            Assert.Null(Field(payload, "taskId"));
            Assert.Equal(GitPrStates.Merged, Field(payload, "state"));
        }

        [Fact]
        public async Task AMatchedCommit_StillCarriesItsTaskId()
        {
            // A másik fele: a kiküldés kinyitása nem vehette el a hozzárendelt sor taskját.
            // A payload-építő átállt a sor SAJÁT TaskId-jére, tehát ha az nem kerül be a
            // sorba, ez a teszt bukik - nem pedig a felhasználó lát azonosító nélküli kártyát.
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (sut, ctx) = ServiceFactory.CreateGitWebhookService(context);

            await sut.ProcessPushEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub, Push("AAA-1 javítás"));

            var payload = SingleCall(ctx.Hub, seed.Project.Id, "CommitLinked");

            Assert.Equal(seed.Task.Id, Field(payload, "taskId"));
            //A kulcs és a cím is a payload része. Azért nem a kliens oldali feladatlistából
            //származtatjuk, mert az a lezárt sprintek feladatait nem tartalmazza - márpedig a
            //hivatkozások nagy része épp a régebbi munkához tartozik.
            Assert.Equal(seed.Task.TaskKey, Field(payload, "taskKey"));
            Assert.Equal(seed.Task.Title, Field(payload, "taskTitle"));
            //Webhookból érkező sor soha nem kézi.
            Assert.Equal(false, Field(payload, "isManuallyLinked"));
        }

        [Fact]
        public async Task AnUnmatchedLink_HasNoTaskKey()
        {
            //A párja: a hozzárendeletlen sornál a kulcs és a cím NULL, nem üres sztring -
            //a git nézet ebből tudja, hogy nincs mit kiírni a jelölőbe.
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (sut, ctx) = ServiceFactory.CreateGitWebhookService(context);

            await sut.ProcessPushEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub, Push("Kulcs nélkül"));

            var payload = SingleCall(ctx.Hub, seed.Project.Id, "CommitLinked");

            Assert.Null(Field(payload, "taskKey"));
            Assert.Null(Field(payload, "taskTitle"));
        }

        [Fact]
        public async Task AMatchedPullRequest_StillCarriesItsTaskId()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (sut, ctx) = ServiceFactory.CreateGitWebhookService(context);

            await sut.ProcessPullRequestEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub, PrEvent("AAA-1 javítás"));

            var payload = SingleCall(ctx.Hub, seed.Project.Id, "PrLinked");

            Assert.Equal(seed.Task.Id, Field(payload, "taskId"));
            Assert.Equal(seed.Task.TaskKey, Field(payload, "taskKey"));
            Assert.Equal(false, Field(payload, "isManuallyLinked"));
        }

        [Fact]
        public async Task WhenAnUnmatchedCommitLaterMatches_TheMatchedRowIsBroadcast()
        {
            // A "helyőrző megszűnik" eset (b). A szerver TÖRLI a hozzárendeletlen sort, és a
            // törlésről nincs külön esemény - szándékosan: a kliens a kapcsolt sor payloadjából
            // kapott sha alapján veszi ki a hozzárendeletlen párját. Ez a teszt azt rögzíti,
            // hogy a kapcsolt kiküldés tényleg megjön, és a sha-t hordozza.
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (sut, ctx) = ServiceFactory.CreateGitWebhookService(context);

            await sut.ProcessPushEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub, Push("Kulcs nélkül"));

            ctx.Hub.Clear();

            //Force push ugyanarra a sha-ra, most már kulccsal
            await sut.ProcessPushEventAsync(
                seed.Project.Id, integration.Id, GitProviders.GitHub, Push("AAA-1 mégis"));

            var payload = SingleCall(ctx.Hub, seed.Project.Id, "CommitLinked");

            Assert.Equal(seed.Task.Id, Field(payload, "taskId"));
            Assert.Equal(Sha, Field(payload, "commitSha"));
        }
    }
}
