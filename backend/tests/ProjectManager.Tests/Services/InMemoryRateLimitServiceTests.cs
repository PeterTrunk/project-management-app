using Microsoft.Extensions.Caching.Memory;
using ProjectManager.API.Services.RateLimit;

namespace ProjectManager.Tests.Services
{
    /// <summary>
    /// Az <see cref="InMemoryRateLimitService"/> tesztjei.
    ///
    /// Azért van saját tesztkörük, mert ez az implementáció a Redis-es változat
    /// **helyettesítője** egypéldányos üzemben: ha itt csendben rosszul számol, a
    /// bejelentkezés brute force védelme szűnik meg anélkül, hogy bármi hibázna.
    /// A Redis-es változatot ugyanez nem fenyegeti - ott a Lua script atomicitása
    /// és a kulcs lejárata a szerver dolga.
    ///
    /// Három dolog mérendő, és mindhárom elromlása csendes lenne:
    /// a számláló megosztottsága (Scoped regisztrációval minden kérés friss vödröt kapna),
    /// az ablak lejárata, és a párhuzamos növelés atomicitása.
    /// </summary>
    public class InMemoryRateLimitServiceTests
    {
        private static InMemoryRateLimitService CreateSut() =>
            new(new MemoryCache(new MemoryCacheOptions()));

        [Fact]
        public async Task IsRateLimited_UnderTheLimit_DoesNotLimit()
        {
            var sut = CreateSut();

            for (var i = 0; i < 5; i++)
            {
                var (isLimited, retryAfter) = await sut.IsRateLimitedAsync("k", 5, TimeSpan.FromMinutes(15));
                Assert.False(isLimited);
                Assert.Equal(0, retryAfter);
            }
        }

        [Fact]
        public async Task IsRateLimited_OverTheLimit_Limits()
        {
            var sut = CreateSut();

            for (var i = 0; i < 5; i++)
                await sut.IsRateLimitedAsync("k", 5, TimeSpan.FromMinutes(15));

            var (isLimited, retryAfter) = await sut.IsRateLimitedAsync("k", 5, TimeSpan.FromMinutes(15));

            Assert.True(isLimited);
            Assert.InRange(retryAfter, 1, (int)TimeSpan.FromMinutes(15).TotalSeconds);
        }

        [Fact]
        public async Task IsRateLimited_DifferentKeys_CountIndependently()
        {
            //Ez a login:{ip}:{email} és a login_ip:{ip} szétválasztásának az alapja:
            //ha a kulcsok összefolynának, egy felhasználó kizárná a többit.
            var sut = CreateSut();

            for (var i = 0; i < 6; i++)
                await sut.IsRateLimitedAsync("login:1.2.3.4:a@example.com", 5, TimeSpan.FromMinutes(15));

            var (otherLimited, _) = await sut.IsRateLimitedAsync("login:1.2.3.4:b@example.com", 5, TimeSpan.FromMinutes(15));

            Assert.False(otherLimited);
        }

        [Fact]
        public async Task IsRateLimited_AfterTheWindowExpires_AllowsAgain()
        {
            //Rövid ablak, hogy a teszt ne legyen lassú. A lejáratot az IMemoryCache
            //absolute expirationja adja - ugyanaz, mint Redisnél az EXPIRE.
            var sut = CreateSut();
            var window = TimeSpan.FromMilliseconds(300);

            for (var i = 0; i < 2; i++)
                await sut.IsRateLimitedAsync("k", 1, window);

            var (limitedBefore, _) = await sut.IsRateLimitedAsync("k", 1, window);
            Assert.True(limitedBefore);

            await Task.Delay(window + TimeSpan.FromMilliseconds(400));

            var (limitedAfter, _) = await sut.IsRateLimitedAsync("k", 1, window);
            Assert.False(limitedAfter);
        }

        [Fact]
        public async Task IsRateLimited_CounterIsSharedAcrossCalls()
        {
            //Ez a Scoped/Singleton csapda tesztje: ha a példány kérésenként új lenne,
            //vagy a cache bejegyzés nem ugyanazt a referenciát adná vissza, a korlát
            //soha nem lépne életbe - és semmi nem hibázna.
            var sut = CreateSut();

            await sut.IsRateLimitedAsync("k", 2, TimeSpan.FromMinutes(1));
            await sut.IsRateLimitedAsync("k", 2, TimeSpan.FromMinutes(1));
            var (isLimited, _) = await sut.IsRateLimitedAsync("k", 2, TimeSpan.FromMinutes(1));

            Assert.True(isLimited);
        }

        [Fact]
        public async Task IsRateLimited_ConcurrentCalls_LosesNoIncrement()
        {
            //A Redis Lua script atomikus. Itt ugyanezt az Interlocked.Increment adja.
            //Olvasás-módosítás-írás mellett a párhuzamos hívások egy része elveszne, és a
            //tényleges korlát a limit felett lenne - pont a támadás pillanatában.
            var sut = CreateSut();
            const int requests = 200;
            const int maxAttempts = 50;

            var results = await Task.WhenAll(Enumerable.Range(0, requests).Select(_ =>
                sut.IsRateLimitedAsync("k", maxAttempts, TimeSpan.FromMinutes(1))));

            //Pontosan az első maxAttempts kérés megy át, a többi limitált
            Assert.Equal(maxAttempts, results.Count(r => !r.IsLimited));
            Assert.Equal(requests - maxAttempts, results.Count(r => r.IsLimited));
        }

        [Fact]
        public async Task IsRateLimited_WhenLimited_RetryAfterIsNeverZero()
        {
            //A Retry-After fejlécben a 0 értelmetlen: a kliens azonnal újrapróbálna.
            var sut = CreateSut();
            var window = TimeSpan.FromMilliseconds(1);

            await sut.IsRateLimitedAsync("k", 1, window);
            await Task.Delay(5);

            //Az ablak itt már lejárhatott; ha a bejegyzés mégis él, a hátralévő idő negatív lenne
            for (var i = 0; i < 3; i++)
            {
                var (isLimited, retryAfter) = await sut.IsRateLimitedAsync("k", 1, window);
                if (isLimited)
                    Assert.True(retryAfter >= 1, $"A RetryAfter {retryAfter} volt, pedig legalább 1 kellene.");
            }
        }
    }
}
