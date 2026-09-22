using Microsoft.EntityFrameworkCore;
using OtpNet;
using ProjectManager.API.Data;
using ProjectManager.API.DTOs.Auth;
using ProjectManager.API.Model;
using ProjectManager.API.Services.EncryptionService;
using ProjectManager.IntegrationTests.Infrastructure;

namespace ProjectManager.IntegrationTests.Auth
{
    /// <summary>
    /// A TOTP titok nyugalmi állapotban titkosítva áll.
    ///
    /// Miért pont ez a titok: az ENCRYPTION_KEY a környezeti változóban él, nem az
    /// adatbázisban, tehát egy adatbázis-oldali szivárgás nem adja meg. A TOTP titok pedig az
    /// egyetlen olyan érték a táblában, ami ÁLLANDÓ (sosem jár le) és a jelszótól FÜGGETLEN -
    /// pont az a dolga, hogy túlélje a jelszó kompromittálódását. Plaintextben tárolva ezt
    /// nem tudja megtenni.
    /// </summary>
    public class TotpSecretEncryptionTests : DatabaseTestBase
    {
        public TotpSecretEncryptionTests(PostgresFixture fixture) : base(fixture) { }

        private const string Password = "Teszt-Jelszo-123";

        /// <summary>Felhasználó valódi BCrypt hash-sel, hogy a bejelentkezés is mérhető legyen.</summary>
        private static async Task<User> SeedUserAsync(AppDbContext context, string email = "totp@example.com")
        {
            var user = new User
            {
                Email = email,
                DisplayName = "Teszt Elek",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password, 12),
                IsEmailVerified = true
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            return user;
        }

        private async Task<string> StoredSecretAsync(Guid userId)
        {
            await using var verify = CreateContext();
            var user = await verify.Users.SingleAsync(u => u.Id == userId);
            return user.TotpSecret!;
        }

        [Fact]
        public async Task SetupTotp_StoresTheSecretEncrypted()
        {
            await using var context = CreateContext();
            var user = await SeedUserAsync(context);
            var (sut, _) = ServiceFactory.CreateAuthService(context, user.Id);

            var setup = await sut.SetupTotpAsync();

            var stored = await StoredSecretAsync(user.Id);

            //A felhasználónak adott base32 titok nem szerepelhet nyersen az adatbázisban
            Assert.True(EncryptionService.IsEncrypted(stored));
            Assert.NotEqual(setup.SecretKey, stored);
            Assert.DoesNotContain(setup.SecretKey, stored);
        }

        /// <summary>
        /// A körbeérés próbája: a hitelesítő alkalmazás a KAPOTT titokból számol kódot, a
        /// szerver a TÁROLT értékből ellenőrzi. Ha a titkosítás elrontaná bármelyik irányt,
        /// a 2FA bekapcsolása elbukna.
        /// </summary>
        [Fact]
        public async Task VerifyAndEnableTotp_WorksWithTheEncryptedSecret()
        {
            await using var context = CreateContext();
            var user = await SeedUserAsync(context);
            var (sut, _) = ServiceFactory.CreateAuthService(context, user.Id);

            var setup = await sut.SetupTotpAsync();
            var code = new Totp(Base32Encoding.ToBytes(setup.SecretKey)).ComputeTotp();

            Assert.True(await sut.VerifyAndEnableTotpAsync(code));

            await using var verify = CreateContext();
            Assert.True((await verify.Users.SingleAsync(u => u.Id == user.Id)).IsTotpEnabled);
        }

        [Fact]
        public async Task LoginWithTotp_WorksWithTheEncryptedSecret()
        {
            await using var context = CreateContext();
            var user = await SeedUserAsync(context);
            var (sut, _) = ServiceFactory.CreateAuthService(context, user.Id);

            var setup = await sut.SetupTotpAsync();
            var totp = new Totp(Base32Encoding.ToBytes(setup.SecretKey));
            await sut.VerifyAndEnableTotpAsync(totp.ComputeTotp());

            var response = await sut.LoginWithTotpAsync(new LoginWithTotpDto
            {
                Email = user.Email,
                Password = Password,
                TotpToken = totp.ComputeTotp()
            });

            Assert.False(string.IsNullOrEmpty(response.Token));
        }

        /// <summary>
        /// A titkosítás bevezetése előtt mentett titkok prefix nélkül, NYERSEN állnak az
        /// adatbázisban. Ezeket nem szabad megpróbálni visszafejteni: a base32 szöveg átmegy a
        /// base64 dekódoláson, a Decrypt szeletelése viszont kivétellel szállna el - és a
        /// felhasználó nem tudna bejelentkezni.
        ///
        /// Ez a teszt a megengedő olvasást őrzi. Amíg létezhet ilyen sor, az ágnak maradnia kell.
        /// </summary>
        [Fact]
        public async Task LegacyPlaintextSecret_StillAuthenticates()
        {
            await using var context = CreateContext();
            var user = await SeedUserAsync(context);

            //Kézzel, a szolgáltatás megkerülésével - pontosan úgy, ahogy a régi kód mentette
            var secretKey = KeyGeneration.GenerateRandomKey(20);
            var base32Secret = Base32Encoding.ToString(secretKey);
            user.TotpSecret = base32Secret;
            user.IsTotpEnabled = true;
            await context.SaveChangesAsync();

            var (sut, _) = ServiceFactory.CreateAuthService(context, user.Id);

            var response = await sut.LoginWithTotpAsync(new LoginWithTotpDto
            {
                Email = user.Email,
                Password = Password,
                TotpToken = new Totp(secretKey).ComputeTotp()
            });

            Assert.False(string.IsNullOrEmpty(response.Token));
        }

        /// <summary>
        /// A fióktörlés is a tárolt titokból ellenőriz - ez a negyedik olvasási hely,
        /// és a legkönnyebben kifelejthető.
        /// </summary>
        [Fact]
        public async Task DeleteAccount_ChecksTheEncryptedSecret()
        {
            await using var context = CreateContext();
            var user = await SeedUserAsync(context);
            var (sut, _) = ServiceFactory.CreateAuthService(context, user.Id);

            var setup = await sut.SetupTotpAsync();
            var totp = new Totp(Base32Encoding.ToBytes(setup.SecretKey));
            await sut.VerifyAndEnableTotpAsync(totp.ComputeTotp());

            await sut.DeleteAccountAsync(new DeleteAccountDto
            {
                CurrentPassword = Password,
                TotpToken = totp.ComputeTotp()
            });

            await using var verify = CreateContext();
            var deleted = await verify.Users.SingleAsync(u => u.Id == user.Id);

            Assert.NotNull(deleted.DeletedAt);
            Assert.Null(deleted.TotpSecret);
        }
    }
}
