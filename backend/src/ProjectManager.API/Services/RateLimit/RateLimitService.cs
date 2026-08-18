using StackExchange.Redis;

namespace ProjectManager.API.Services.RateLimit
{
    public class RateLimitService : IRateLimitService
    {
        private readonly IConnectionMultiplexer _redis;

        public RateLimitService(IConnectionMultiplexer redis)
        {
            _redis = redis;
        }

        public async Task<(bool IsLimited, int RetryAfterSeconds)> IsRateLimitedAsync(string key, int maxAttempts, TimeSpan window)
        {
            var db = _redis.GetDatabase();
            var count = await db.StringIncrementAsync(key);
            if (count == 1)
                await db.KeyExpireAsync(key, window);

            if (count > maxAttempts)
            {
                var ttl = await db.KeyTimeToLiveAsync(key);
                return (true, (int)(ttl?.TotalSeconds ?? window.TotalSeconds));
            }
            return (false, 0);
        }
    }
}
