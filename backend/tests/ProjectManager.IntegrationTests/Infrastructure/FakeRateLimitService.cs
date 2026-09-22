using ProjectManager.API.Services.RateLimit;

namespace ProjectManager.IntegrationTests.Infrastructure
{
    /// <summary>
    /// A rate limit élesben Redisre épül. A tesztekben nem azt mérjük, hogy a Redis jól számol -
    /// hanem azt, hogy a szolgáltatás a HELYES KULCSOKRA kérdez rá.
    ///
    /// Ezért a dupla alapból mindent átenged, rögzíti a kulcsokat, és kérésre kikapcsolja
    /// bármelyiket. Egy valódi Redis ehhez csak lassabb lenne, és nem árulná el, mire kérdeztünk.
    /// </summary>
    public sealed class FakeRateLimitService : IRateLimitService
    {
        private readonly List<string> _keys = new();
        private readonly HashSet<string> _limited = new();

        /// <summary>A megkérdezett kulcsok, hívási sorrendben.</summary>
        public IReadOnlyList<string> Keys => _keys;

        /// <summary>Ez a kulcs mostantól korlátozottnak számít.</summary>
        public void Limit(string key) => _limited.Add(key);

        /// <summary>Megkérdeztük-e ezt a pontos kulcsot?</summary>
        public bool Asked(string key) => _keys.Contains(key);

        /// <summary>Megkérdeztük-e olyan kulcsot, ami ezzel az előtaggal kezdődik?</summary>
        public bool AskedStartingWith(string prefix) =>
            _keys.Any(k => k.StartsWith(prefix, StringComparison.Ordinal));

        public void Clear()
        {
            _keys.Clear();
            _limited.Clear();
        }

        public Task<(bool IsLimited, int RetryAfterSeconds)> IsRateLimitedAsync(
            string key, int maxAttempts, TimeSpan window)
        {
            _keys.Add(key);

            return Task.FromResult(_limited.Contains(key)
                ? (true, (int)window.TotalSeconds)
                : (false, 0));
        }
    }
}
