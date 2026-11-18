using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.Common.Exceptions;
using ProjectManager.API.DTOs.Integration;
using ProjectManager.API.Model;
using ProjectManager.API.Services.EncryptionService;
using ProjectManager.IntegrationTests.Infrastructure;
using System.Text.Json;

namespace ProjectManager.IntegrationTests.GitWebhook
{
    /// <summary>
    /// A hozzáférési token tárolása és az automatikus pótlás kapcsolója.
    ///
    /// Ez az oszlop egyszer már létezett, és tudatosan töröltük: egy sosem használt hitelesítő
    /// adat törlése jobb, mint a titkosítása. A döntés feltétele változott meg - a szolgáltatók
    /// levágják a webhook payload commit tömbjét, a kimaradt commitok pedig csak az API-n
    /// kérhetők le -, ezért a token visszatér, de most TITKOSÍTVA.
    ///
    /// A tesztek súlypontja ennek megfelelően nem az, hogy eltárolódik-e, hanem hogy milyen
    /// alakban tárolódik, és hogy nem szivárog-e ki.
    /// </summary>
    public class IntegrationAccessTokenTests : DatabaseTestBase
    {
        public IntegrationAccessTokenTests(PostgresFixture fixture) : base(fixture) { }

        private const string Token = "ghp_pELdAtokEN1234567890abcdefghijklmn";

        private static CreateIntegrationDto CreateDto(
            string? accessToken = null, bool autoBackfill = false) => new()
            {
                Provider = GitProviders.GitHub,
                RepoFullName = "peter/pma",
                WebhookSecret = "legalabb-tizenhat-karakter",
                AccessToken = accessToken,
                AutoBackfillEnabled = autoBackfill,
                AuthorityConfirmed = true
            };

        private async Task<Integration> ReloadAsync(Guid integrationId)
        {
            await using var verify = CreateContext();
            return await verify.Integrations.SingleAsync(i => i.Id == integrationId);
        }

        [Fact]
        public async Task CreateWithAccessToken_StoresItEncrypted()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var (sut, _) = ServiceFactory.CreateIntegrationService(context, seed.Owner.Id);

            var response = await sut.CreateIntegrationAsync(seed.Project.Id, CreateDto(Token));

            var stored = (await ReloadAsync(response.Id)).AccessToken;

            Assert.NotNull(stored);
            Assert.True(EncryptionService.IsEncrypted(stored));
            Assert.DoesNotContain(Token, stored);
            Assert.True(response.HasAccessToken);
        }

        [Fact]
        public async Task CreateWithoutAccessToken_LeavesItEmpty()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var (sut, _) = ServiceFactory.CreateIntegrationService(context, seed.Owner.Id);

            var response = await sut.CreateIntegrationAsync(seed.Project.Id, CreateDto());

            Assert.Null((await ReloadAsync(response.Id)).AccessToken);
            Assert.False(response.HasAccessToken);

            //A pótlás kvótát éget, ezért a bekapcsolás tudatos döntés kell legyen
            Assert.False(response.AutoBackfillEnabled);
        }

        /// <summary>
        /// Az üres és a hiányzó token NEM két állapot. Enélkül az üres szöveg titkosított alakja
        /// érvényes ciphertext lenne, tehát a rendszer azt hinné, van tokenje - és a pótlás egy
        /// üres hitelesítő adattal indulna el a szolgáltató felé.
        /// </summary>
        [Fact]
        public async Task CreateWithBlankAccessToken_IsTheSameAsNone()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var (sut, _) = ServiceFactory.CreateIntegrationService(context, seed.Owner.Id);

            var response = await sut.CreateIntegrationAsync(seed.Project.Id, CreateDto("   "));

            Assert.Null((await ReloadAsync(response.Id)).AccessToken);
            Assert.False(response.HasAccessToken);
        }

        /// <summary>
        /// A legfontosabb állítás: a token egyetlen válaszban sem jelenik meg, sem nyersen,
        /// sem titkosítva. A DTO egész szerializált alakját vizsgáljuk, nem egy kiválasztott
        /// mezőt - egy később hozzáadott mező így sem tudja kivinni az értéket.
        /// </summary>
        [Fact]
        public async Task TheTokenNeverLeavesTheServer()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var (sut, _) = ServiceFactory.CreateIntegrationService(context, seed.Owner.Id);

            var created = await sut.CreateIntegrationAsync(seed.Project.Id, CreateDto(Token));
            var stored = (await ReloadAsync(created.Id)).AccessToken!;
            var listed = await sut.GetIntegrationsAsync(seed.Project.Id);

            foreach (var json in new[] { JsonSerializer.Serialize(created), JsonSerializer.Serialize(listed) })
            {
                Assert.DoesNotContain(Token, json);
                Assert.DoesNotContain(stored, json);
            }
        }

        [Fact]
        public async Task SetAccessToken_ReplacesTheStoredValueAndLogsTheFact()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (sut, ctx) = ServiceFactory.CreateIntegrationService(context, seed.Owner.Id);

            await sut.SetAccessTokenAsync(seed.Project.Id, integration.Id, Token);

            var stored = (await ReloadAsync(integration.Id)).AccessToken;
            Assert.True(EncryptionService.IsEncrypted(stored));
            Assert.True(ctx.Hub.SentToProject(seed.Project.Id, "IntegrationUpdated"));

            await using var verify = CreateContext();
            var activity = await verify.Activities.SingleAsync(a =>
                a.EntityType == "Integration" && a.Action == "AccessTokenSet");

            //A napló a TÉNYT rögzíti, az értéket soha
            Assert.DoesNotContain(Token, activity.Description);
        }

        [Fact]
        public async Task SetAccessToken_WithAnEmptyValue_RemovesIt()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (sut, _) = ServiceFactory.CreateIntegrationService(context, seed.Owner.Id);

            await sut.SetAccessTokenAsync(seed.Project.Id, integration.Id, Token);
            await sut.SetAccessTokenAsync(seed.Project.Id, integration.Id, "");

            Assert.Null((await ReloadAsync(integration.Id)).AccessToken);

            await using var verify = CreateContext();
            Assert.True(await verify.Activities.AnyAsync(a => a.Action == "AccessTokenRemoved"));
        }

        [Fact]
        public async Task SetAutoBackfill_TogglesTheIntegrationAndLogsIt()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (sut, ctx) = ServiceFactory.CreateIntegrationService(context, seed.Owner.Id);

            await sut.SetAutoBackfillAsync(seed.Project.Id, integration.Id, true);
            Assert.True((await ReloadAsync(integration.Id)).AutoBackfillEnabled);

            await sut.SetAutoBackfillAsync(seed.Project.Id, integration.Id, false);
            Assert.False((await ReloadAsync(integration.Id)).AutoBackfillEnabled);

            Assert.True(ctx.Hub.SentToProject(seed.Project.Id, "IntegrationUpdated"));

            await using var verify = CreateContext();
            Assert.Equal(2, await verify.Activities.CountAsync(a =>
                a.Action == "AutoBackfillEnabled" || a.Action == "AutoBackfillDisabled"));
        }

        /// <summary>
        /// A projekt hatókör a hitelesítő adatot kezelő végpontokon sem gyengülhet: a másik
        /// projekt integrációja nem létezőnek látszik, nem jogosulatlannak.
        /// </summary>
        [Fact]
        public async Task SetAccessToken_FromAnotherProject_IsNotFound()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var other = await TestData.SeedProjectAsync(context, "BBB");
            var integration = await TestData.SeedIntegrationAsync(context, seed.Project.Id);
            var (sut, _) = ServiceFactory.CreateIntegrationService(context, seed.Owner.Id);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                sut.SetAccessTokenAsync(other.Project.Id, integration.Id, Token));

            Assert.Null((await ReloadAsync(integration.Id)).AccessToken);
        }
    }
}
