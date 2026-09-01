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

        public async Task<(bool IsLimited, int RetryAfterSeconds)> IsRateLimitedAsync(
            string key, 
            int maxAttempts, 
            TimeSpan window)
        {
            var db = _redis.GetDatabase();

            //Atomikus Lua script - INCR + EXPIRE egy műveletben
            //Azért szükséges, mert a két Redis async parancs INCR és EXPIRE külön hívásokként
            //nem atomikus, ha köztük megszakad a kapcsolat (timeout, deploy/restart),
            //a kulcs TTL nélkül marad Redisben és az adott email/IP véglegesen limitáltá válhatna
            //Megoldás: Lua script futattása Redisen, ahol egyben kezeljük a két dolgot.
            //Így mindent vagy semmit elv alapján vagy mindkettő megvan vagy sem.
            var script = @"
                local count = redis.call('INCR', KEYS[1])
                if count == 1 then
                    redis.call('EXPIRE', KEYS[1], ARGV[1])
                end
                return count
            ";

            var count = (long)await db.ScriptEvaluateAsync(
                script,
                new RedisKey[] { key },
                new RedisValue[] { (long)window.TotalSeconds }
            );

            if (count > maxAttempts)
            {
                var ttl = await db.KeyTimeToLiveAsync(key);
                return (true, (int)(ttl?.TotalSeconds ?? window.TotalSeconds));
            }

            return (false, 0);
        }
    }
}
