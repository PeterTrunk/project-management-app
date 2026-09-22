using ProjectManager.API.Common.Constants;
using ProjectManager.IntegrationTests.Infrastructure;

namespace ProjectManager.IntegrationTests.GitWebhook
{
    /// <summary>
    /// Az integráció megkeresése a webhook tokenje alapján.
    ///
    /// A metódus a LETILTOTT integrációt is visszaadja, és a szűrés a hívóé. Ez nem
    /// figyelmetlenség, hanem a lényeg: korábban maga a lekérdezés szűrt az `IsEnabled`-re,
    /// így a „nincs ilyen token" és a „letiltott integráció" ugyanazt a nullát adta. A
    /// végpont mindkettőre `401`-et válaszolt, naplóba pedig semmi nem került - egy hibás
    /// beállítás okát csak találgatni lehetett.
    ///
    /// A válasz továbbra is azonos mindkét esetben (egy eltérő üzenetből ki lehetne deríteni,
    /// létezik-e egy adott token), a KÜLÖNBSÉG a naplóba kerül.
    ///
    /// Ha valaki visszateszi a szűrőt a lekérdezésbe, ezek a tesztek buknak - és a
    /// WebhookController letiltás-ága némán halott kóddá válna.
    /// </summary>
    public class WebhookTokenLookupTests : DatabaseTestBase
    {
        public WebhookTokenLookupTests(PostgresFixture fixture) : base(fixture) { }

        [Fact]
        public async Task EnabledIntegration_IsFound()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (sut, _) = ServiceFactory.CreateIntegrationService(context, seed.Owner.Id);

            var found = await sut.GetByWebhookTokenAsync(integration.WebhookToken);

            Assert.NotNull(found);
            Assert.Equal(integration.Id, found!.Id);
        }

        /// <summary>
        /// A hívó ebből tudja, hogy a token JÓ, csak az integráció van kikapcsolva - és ezt
        /// naplózhatja, mielőtt elutasít.
        /// </summary>
        [Fact]
        public async Task DisabledIntegration_IsStillFound()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id, GitProviders.GitLab);

            //UGYANEZEN a contexten tiltjuk le. Kulon contextbol mentve a lekerdezes a change
            //tracker szerinti - meg engedelyezett - peldanyt adna vissza, es a teszt azt merne,
            //mit tart a memoria, nem azt, mi van az adatbazisban.
            integration.IsEnabled = false;
            await context.SaveChangesAsync();

            var (sut, _) = ServiceFactory.CreateIntegrationService(context, seed.Owner.Id);

            var found = await sut.GetByWebhookTokenAsync(integration.WebhookToken);

            Assert.NotNull(found);
            Assert.False(found!.IsEnabled);
        }

        [Fact]
        public async Task UnknownToken_IsNotFound()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (sut, _) = ServiceFactory.CreateIntegrationService(context, seed.Owner.Id);

            Assert.Null(await sut.GetByWebhookTokenAsync("ez-a-token-sosem-letezett"));
        }

        /// <summary>
        /// A projekt betöltve érkezik: a feldolgozás a ProjectId-ra épül, és egy külön
        /// lekérdezés minden egyes webhook eseménynél fölösleges kör lenne.
        /// </summary>
        [Fact]
        public async Task FoundIntegration_CarriesItsProject()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (sut, _) = ServiceFactory.CreateIntegrationService(context, seed.Owner.Id);

            var found = await sut.GetByWebhookTokenAsync(integration.WebhookToken);

            Assert.NotNull(found!.Project);
            Assert.Equal(seed.Project.Id, found.ProjectId);
        }
    }
}
