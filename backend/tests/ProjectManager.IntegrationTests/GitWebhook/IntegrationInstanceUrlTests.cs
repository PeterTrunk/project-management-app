using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.Common.Exceptions;
using ProjectManager.API.DTOs.Integration;
using ProjectManager.API.Model;
using ProjectManager.IntegrationTests.Infrastructure;

namespace ProjectManager.IntegrationTests.GitWebhook
{
    /// <summary>
    /// A saját telepítésű szolgáltató címe.
    ///
    /// A webhook irányban erre nincs szükség - a szolgáltató hív minket, bárhol is álljon -,
    /// ezért eddig nem is létezett ez a mező. A pótlás viszont KIMENŐ hívás: ott nekünk kell
    /// tudnunk, melyik kiszolgálót kérdezzük, és egy céges GitLab címe nem a gitlab.com.
    /// </summary>
    public class IntegrationInstanceUrlTests : DatabaseTestBase
    {
        public IntegrationInstanceUrlTests(PostgresFixture fixture) : base(fixture) { }

        private static CreateIntegrationDto CreateDto(string? instanceUrl = null) => new()
        {
            Provider = GitProviders.GitLab,
            RepoFullName = "ceg/csapat/pma",
            WebhookSecret = "legalabb-tizenhat-karakter",
            InstanceUrl = instanceUrl,
            AuthorityConfirmed = true
        };

        private async Task<Integration> ReloadAsync(Guid integrationId)
        {
            await using var verify = CreateContext();
            return await verify.Integrations.SingleAsync(i => i.Id == integrationId);
        }

        /// <summary>
        /// A záró perjel levágása nem kozmetika: a verziós útvonal hozzáfűzésével kettős
        /// perjeles cím keletkezne, amitől a szolgáltató más végpontot látna.
        /// </summary>
        [Fact]
        public async Task CreateWithAnInstanceUrl_StoresItWithoutATrailingSlash()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var (sut, _) = ServiceFactory.CreateIntegrationService(context, seed.Owner.Id);

            var response = await sut.CreateIntegrationAsync(
                seed.Project.Id, CreateDto("https://git.ceg.hu/"));

            Assert.Equal("https://git.ceg.hu", (await ReloadAsync(response.Id)).InstanceUrl);

            //A cím nem titok: a felületnek meg kell tudnia jeleníteni, melyik kiszolgálóval dolgozik
            Assert.Equal("https://git.ceg.hu", response.InstanceUrl);
        }

        [Fact]
        public async Task CreateWithoutAnInstanceUrl_MeansTheCloud()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var (sut, _) = ServiceFactory.CreateIntegrationService(context, seed.Owner.Id);

            var response = await sut.CreateIntegrationAsync(seed.Project.Id, CreateDto());

            Assert.Null((await ReloadAsync(response.Id)).InstanceUrl);
            Assert.Null(response.InstanceUrl);
        }

        [Fact]
        public async Task SetInstanceUrl_UpdatesTheIntegrationAndLogsIt()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id, GitProviders.GitLab);
            var (sut, ctx) = ServiceFactory.CreateIntegrationService(context, seed.Owner.Id);

            await sut.SetInstanceUrlAsync(seed.Project.Id, integration.Id, "https://git.ceg.hu");

            Assert.Equal("https://git.ceg.hu", (await ReloadAsync(integration.Id)).InstanceUrl);
            Assert.True(ctx.Hub.SentToProject(seed.Project.Id, "IntegrationUpdated"));

            await using var verify = CreateContext();
            Assert.True(await verify.Activities.AnyAsync(a => a.Action == "InstanceUrlSet"));
        }

        [Fact]
        public async Task SetInstanceUrl_WithAnEmptyValue_FallsBackToTheCloud()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id, GitProviders.GitLab);
            var (sut, _) = ServiceFactory.CreateIntegrationService(context, seed.Owner.Id);

            await sut.SetInstanceUrlAsync(seed.Project.Id, integration.Id, "https://git.ceg.hu");
            await sut.SetInstanceUrlAsync(seed.Project.Id, integration.Id, "");

            Assert.Null((await ReloadAsync(integration.Id)).InstanceUrl);

            await using var verify = CreateContext();
            Assert.True(await verify.Activities.AnyAsync(a => a.Action == "InstanceUrlRemoved"));
        }

        [Fact]
        public async Task SetInstanceUrl_FromAnotherProject_IsNotFound()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var other = await TestData.SeedProjectAsync(context, "BBB");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id, GitProviders.GitLab);
            var (sut, _) = ServiceFactory.CreateIntegrationService(context, seed.Owner.Id);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                sut.SetInstanceUrlAsync(other.Project.Id, integration.Id, "https://git.ceg.hu"));

            Assert.Null((await ReloadAsync(integration.Id)).InstanceUrl);
        }
    }
}
