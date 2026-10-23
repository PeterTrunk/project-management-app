using Microsoft.Extensions.Caching.Memory;

namespace ProjectManager.API.Services.RateLimit
{
    /// <summary>
    /// Kérelemkorlátozás a folyamat memóriájában, Redis nélkül.
    /// <para>
    /// Kizárólag EGYETLEN API példány esetén helyes, ezért a regisztráció az
    /// <c>API_REPLICAS</c> értékéhez van kötve (lásd <c>ServiceCollectionExtensions</c>).
    /// Több példány mellett minden példány a saját számlálóit vezetné, tehát a tényleges
    /// korlát a példányszámmal felszorozódna - csendben. Ezért több replikánál a Redis
    /// kötelező, és a hiánya induláskor hibát dob.
    /// </para>
    /// <para>
    /// Szándékosan ugyanazt a fix ablakos szemantikát valósítja meg, mint a Redis-es
    /// változat Lua scriptje: az első kérés indítja az ablakot, a lejárat az ablak
    /// hosszával egyezik, és a számláló nem csúszik.
    /// </para>
    /// </summary>
    public class InMemoryRateLimitService : IRateLimitService
    {
        //A kulcsok IP címet és e-mail címet tartalmaznak, tehát a kardinalitásuk korlátlan.
        //Lejárat nélkül ez memóriaszivárgás lenne, ezért IMemoryCache: az absolute
        //expiration pontosan a Lua script EXPIRE-jának felel meg, és a takarítás a cache
        //dolga, nem a miénk.
        private readonly IMemoryCache _cache;

        public InMemoryRateLimitService(IMemoryCache cache)
        {
            _cache = cache;
        }

        /// <summary>Egy ablak állapota. Osztály, hogy a cache-ben a referencia maradjon ugyanaz.</summary>
        private sealed class Window
        {
            public int Count;
            public DateTimeOffset ExpiresAt;
        }

        public Task<(bool IsLimited, int RetryAfterSeconds)> IsRateLimitedAsync(
            string key,
            int maxAttempts,
            TimeSpan window)
        {
            //A Lua script atomikus. Itt ugyanezt két dolog adja: a bejegyzés létrehozása
            //zárolás alatt történik, a növelés pedig Interlockeddel - vagyis két párhuzamos
            //kérés nem olvashatja ugyanazt a számlálót és írhatja vissza ugyanazt az értéket.
            Window entry;
            lock (_cache)
            {
                if (!_cache.TryGetValue(key, out Window? existing) || existing is null)
                {
                    existing = new Window
                    {
                        Count = 0,
                        ExpiresAt = DateTimeOffset.UtcNow.Add(window)
                    };

                    _cache.Set(key, existing, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpiration = existing.ExpiresAt
                    });
                }

                entry = existing;
            }

            var count = Interlocked.Increment(ref entry.Count);

            if (count > maxAttempts)
            {
                //A hátralévő idő az ablak végéig - ugyanaz, amit Redisnél a KeyTimeToLive ad.
                //Nem lehet negatív: egy épp lejáró bejegyzésnél 0 helyett 1-et adunk, hogy a
                //Retry-After fejléc értelmes maradjon.
                var remaining = (int)Math.Ceiling((entry.ExpiresAt - DateTimeOffset.UtcNow).TotalSeconds);
                return Task.FromResult((true, Math.Max(1, remaining)));
            }

            return Task.FromResult((false, 0));
        }
    }
}
