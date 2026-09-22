using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.Model;
using ProjectManager.IntegrationTests.Infrastructure;

namespace ProjectManager.IntegrationTests.GitWebhook
{
    /// <summary>
    /// Az integráció verifikálása.
    ///
    /// A GitHubnak van <c>ping</c> eseménye, amit a webhook felvételekor azonnal küld - a
    /// GitLabnak NINCS: ott a „Test" gomb is valódi push eseményt küld. Ezért a verifikáció
    /// nem köthető a pinghez, különben minden GitLab integráció örökre jelöletlen maradna,
    /// miközben a webhook tökéletesen működik.
    ///
    /// A jelölés jelentése nem gyengül attól, hogy egy push is kiváltja: az
    /// aláírás-ellenőrzés addigra lefutott, vagyis a szolgáltató bizonyítottan elér minket,
    /// a helyes titokkal.
    /// </summary>
    public class IntegrationVerificationTests : DatabaseTestBase
    {
        public IntegrationVerificationTests(PostgresFixture fixture) : base(fixture) { }

        private async Task<Integration> ReloadAsync(Guid integrationId)
        {
            await using var verify = CreateContext();
            return await verify.Integrations.SingleAsync(i => i.Id == integrationId);
        }

        private async Task<List<Activity>> VerificationActivitiesAsync(Guid projectId)
        {
            await using var verify = CreateContext();
            return await verify.Activities
                .Where(a => a.ProjectId == projectId && a.EntityType == "Integration" && a.Action == "Verified")
                .ToListAsync();
        }

        [Fact]
        public async Task NewIntegration_StartsUnverified()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id, GitProviders.GitLab);

            Assert.False((await ReloadAsync(integration.Id)).IsVerified);
        }

        [Fact]
        public async Task Verifying_MarksTheIntegrationAndLogsIt()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id, GitProviders.GitLab);
            var (sut, ctx) = ServiceFactory.CreateIntegrationService(context, seed.Owner.Id);

            await sut.VerifyIntegrationAsync(integration.Id);

            Assert.True((await ReloadAsync(integration.Id)).IsVerified);
            Assert.True(ctx.Hub.SentToProject(seed.Project.Id, "IntegrationVerified"));

            var activity = Assert.Single(await VerificationActivitiesAsync(seed.Project.Id));
            //A provider neve a rekordból jön: korábban "GitHub" volt beégetve, tehát egy
            //GitLab integrációról is azt írta volna ki
            Assert.StartsWith("GitLab ", activity.Description);
            Assert.DoesNotContain("GitHub", activity.Description);
        }

        /// <summary>
        /// A hívó mostantól MINDEN sikeresen feldolgozott eseményre meghívhatja, tehát a
        /// metódusnak idempotensnek kell lennie - különben a második webhooktól kezdve
        /// fölösleges mentés és tevékenység-bejegyzés keletkezne.
        /// </summary>
        [Fact]
        public async Task VerifyingTwice_DoesNotLogTwice()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id, GitProviders.GitLab);
            var (sut, ctx) = ServiceFactory.CreateIntegrationService(context, seed.Owner.Id);

            await sut.VerifyIntegrationAsync(integration.Id);
            ctx.Hub.Clear();
            await sut.VerifyIntegrationAsync(integration.Id);
            await sut.VerifyIntegrationAsync(integration.Id);

            Assert.True((await ReloadAsync(integration.Id)).IsVerified);
            Assert.Single(await VerificationActivitiesAsync(seed.Project.Id));
            Assert.Empty(ctx.Hub.CallsToProject(seed.Project.Id));
        }

        [Fact]
        public async Task GitHubIntegration_GetsItsProviderNameInTheActivity()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id, GitProviders.GitHub);
            var (sut, _) = ServiceFactory.CreateIntegrationService(context, seed.Owner.Id);

            await sut.VerifyIntegrationAsync(integration.Id);

            var activity = Assert.Single(await VerificationActivitiesAsync(seed.Project.Id));
            Assert.StartsWith("GitHub ", activity.Description);
        }
    }
}
