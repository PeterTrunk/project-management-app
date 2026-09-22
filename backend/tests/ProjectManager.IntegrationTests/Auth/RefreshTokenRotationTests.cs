using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Common.Exceptions;
using ProjectManager.API.Data;
using ProjectManager.API.DTOs.Auth;
using ProjectManager.API.Model;
using ProjectManager.IntegrationTests.Infrastructure;

namespace ProjectManager.IntegrationTests.Auth
{
    /// <summary>
    /// A refresh token rotáció és a visszajátszás kezelése.
    ///
    /// A védendő forgatókönyv: a támadó megszerez egy refresh tokent (például egy
    /// kiszivárgott mentésből), és rotál vele. A jogos felhasználó legközelebbi
    /// megújítása ekkor elbukik - védelem nélkül viszont csak annyit venne észre, hogy ki
    /// kellett jelentkeznie, miközben a TÁMADÓ tokenje érvényben marad. A visszajátszás
    /// felismerése épp ezt zárja le.
    /// </summary>
    public class RefreshTokenRotationTests : DatabaseTestBase
    {
        public RefreshTokenRotationTests(PostgresFixture fixture) : base(fixture) { }

        private const string Password = "Teszt-Jelszo-123";

        private static async Task<User> SeedUserAsync(AppDbContext context, string email = "auth@example.com")
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

        private static Task<AuthResponseDto> LoginAsync(ProjectManager.API.Services.Auth.AuthService sut, User user) =>
            sut.LoginAsync(new LoginDto { Email = user.Email, Password = Password });

        private async Task<List<RefreshToken>> TokensAsync(Guid userId)
        {
            await using var verify = CreateContext();
            return await verify.RefreshTokens.Where(rt => rt.UserId == userId).ToListAsync();
        }

        //A normál út

        [Fact]
        public async Task Rotation_IssuesANewTokenAndRevokesTheOld()
        {
            await using var context = CreateContext();
            var user = await SeedUserAsync(context);
            var (sut, _) = ServiceFactory.CreateAuthService(context);

            var login = await LoginAsync(sut, user);
            var rotated = await sut.RefreshTokenAsync(login.RefreshToken);

            Assert.NotEqual(login.RefreshToken, rotated.RefreshToken);

            var tokens = await TokensAsync(user.Id);
            Assert.True(tokens.Single(t => t.Token == login.RefreshToken).IsRevoked);
            Assert.False(tokens.Single(t => t.Token == rotated.RefreshToken).IsRevoked);
        }

        [Fact]
        public async Task UnknownToken_IsRejectedWithoutTouchingAnySession()
        {
            await using var context = CreateContext();
            var user = await SeedUserAsync(context);
            var (sut, _) = ServiceFactory.CreateAuthService(context);

            var login = await LoginAsync(sut, user);

            //Találgatás: a sor nem létezik, tehát nincs mit riasztani
            await Assert.ThrowsAsync<ValidationException>(
                () => sut.RefreshTokenAsync("ez-a-token-sosem-letezett"));

            Assert.False(Assert.Single(await TokensAsync(user.Id)).IsRevoked);
            Assert.Equal(login.RefreshToken, (await TokensAsync(user.Id)).Single().Token);
        }

        //A visszajátszás

        /// <summary>
        /// A lényegi eset: egy már elhasznált token újbóli bemutatása lopott token jele.
        /// A válasz a felhasználó ÖSSZES munkamenetének visszavonása - beleértve azt is,
        /// amit a támadó épp most szerzett a rotációval.
        /// </summary>
        [Fact]
        public async Task ReplayingARotatedToken_RevokesEverySession()
        {
            await using var context = CreateContext();
            var user = await SeedUserAsync(context);
            var (sut, _) = ServiceFactory.CreateAuthService(context);

            var login = await LoginAsync(sut, user);
            var rotated = await sut.RefreshTokenAsync(login.RefreshToken);

            //A jogos felhasználó a RÉGI tokenjével próbálkozik, mert a támadó megelőzte
            await Assert.ThrowsAsync<ValidationException>(
                () => sut.RefreshTokenAsync(login.RefreshToken));

            var tokens = await TokensAsync(user.Id);

            Assert.All(tokens, t => Assert.True(t.IsRevoked));
            //Nevesítve: a rotációval szerzett token sem él tovább
            Assert.True(tokens.Single(t => t.Token == rotated.RefreshToken).IsRevoked);
        }

        [Fact]
        public async Task ReplayedToken_CannotBeUsedAgainAfterTheAlarm()
        {
            await using var context = CreateContext();
            var user = await SeedUserAsync(context);
            var (sut, _) = ServiceFactory.CreateAuthService(context);

            var login = await LoginAsync(sut, user);
            var rotated = await sut.RefreshTokenAsync(login.RefreshToken);

            await Assert.ThrowsAsync<ValidationException>(() => sut.RefreshTokenAsync(login.RefreshToken));

            //A támadó tokenje sem működik többé
            await Assert.ThrowsAsync<ValidationException>(() => sut.RefreshTokenAsync(rotated.RefreshToken));
        }

        /// <summary>
        /// A kijelentkezés TÖRLI a sort, nem visszavonja. Enélkül egy ártatlan versenyhelyzet -
        /// a háttérfül a kijelentkezés után még egyszer megpróbálja - riasztást váltana ki,
        /// és a felhasználót MINDEN eszközről kiléptetné.
        /// </summary>
        [Fact]
        public async Task ReplayingALoggedOutToken_DoesNotRaiseTheAlarm()
        {
            await using var context = CreateContext();
            var user = await SeedUserAsync(context);
            var (sut, _) = ServiceFactory.CreateAuthService(context);

            var phone = await LoginAsync(sut, user);
            var desktop = await LoginAsync(sut, user);

            await sut.LogoutAsync(phone.RefreshToken);

            //A kijelentkezett token késői bemutatása
            await Assert.ThrowsAsync<ValidationException>(() => sut.RefreshTokenAsync(phone.RefreshToken));

            //A másik eszköz munkamenete érintetlen
            var stillValid = await sut.RefreshTokenAsync(desktop.RefreshToken);
            Assert.False(string.IsNullOrEmpty(stillValid.Token));
        }

        [Fact]
        public async Task Logout_RemovesTheRow()
        {
            await using var context = CreateContext();
            var user = await SeedUserAsync(context);
            var (sut, _) = ServiceFactory.CreateAuthService(context);

            var login = await LoginAsync(sut, user);
            await sut.LogoutAsync(login.RefreshToken);

            Assert.Empty(await TokensAsync(user.Id));
        }

        //Rate limit kulcsok

        /// <summary>
        /// A szűkebb kulcs az e-mail címet is tartalmazza, tehát minden új címhez új vödör
        /// tartozik - egyetlen gépről így korlátlanul lehetne végigpróbálni egy címlistát.
        /// A tágabb, IP-alapú korlát ezt zárja le.
        /// </summary>
        [Fact]
        public async Task Login_AsksBothTheNarrowAndTheIpWideLimit()
        {
            await using var context = CreateContext();
            var user = await SeedUserAsync(context);
            var (sut, ctx) = ServiceFactory.CreateAuthService(context);

            await LoginAsync(sut, user);

            Assert.True(ctx.RateLimit.Asked($"login:{ctx.IpAddress}:{user.Email}"));
            Assert.True(ctx.RateLimit.Asked($"login_ip:{ctx.IpAddress}"));
        }

        [Fact]
        public async Task Login_IsBlockedWhenTheIpWideLimitTrips()
        {
            await using var context = CreateContext();
            var user = await SeedUserAsync(context);
            var (sut, ctx) = ServiceFactory.CreateAuthService(context);

            //A szűkebb kulcs szabad - csak az IP-alapú fogott
            ctx.RateLimit.Limit($"login_ip:{ctx.IpAddress}");

            await Assert.ThrowsAsync<RateLimitException>(() => LoginAsync(sut, user));
        }

        [Fact]
        public async Task ResendVerification_AsksBothTheNarrowAndTheIpWideLimit()
        {
            await using var context = CreateContext();
            var user = await SeedUserAsync(context, "unverified@example.com");
            user.IsEmailVerified = false;
            await context.SaveChangesAsync();

            var (sut, ctx) = ServiceFactory.CreateAuthService(context);

            await sut.ResendVerificationEmailAsync(user.Email);

            Assert.True(ctx.RateLimit.Asked($"resend_verification:{ctx.IpAddress}:{user.Email}"));
            Assert.True(ctx.RateLimit.Asked($"resend_verification_ip:{ctx.IpAddress}"));

            //A levél tényleg kiment, tokennel együtt
            var sent = ctx.Email.LastOfKind("verification");
            Assert.NotNull(sent);
            Assert.False(string.IsNullOrEmpty(sent!.Token));
        }

        /// <summary>
        /// A jelszóváltás a SEC-04 óta minden munkamenetet visszavon. Ez a teszt azt őrzi,
        /// hogy a kijelentkezés törlésre váltása ezen nem rontott.
        /// </summary>
        [Fact]
        public async Task ChangingThePassword_RevokesEverySession()
        {
            await using var context = CreateContext();
            var user = await SeedUserAsync(context);
            var (sut, _) = ServiceFactory.CreateAuthService(context, user.Id);

            var phone = await LoginAsync(sut, user);
            var desktop = await LoginAsync(sut, user);

            await sut.ChangePasswordAsync(user.Id, new ChangePasswordDto
            {
                CurrentPassword = Password,
                NewPassword = "Masik-Jelszo-456"
            });

            await Assert.ThrowsAsync<ValidationException>(() => sut.RefreshTokenAsync(phone.RefreshToken));
            await Assert.ThrowsAsync<ValidationException>(() => sut.RefreshTokenAsync(desktop.RefreshToken));
        }
    }
}
