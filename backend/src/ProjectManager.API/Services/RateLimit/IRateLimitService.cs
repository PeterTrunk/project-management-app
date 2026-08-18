namespace ProjectManager.API.Services.RateLimit
{
    public interface IRateLimitService
    {
        Task<(bool IsLimited, int RetryAfterSeconds)> IsRateLimitedAsync(string key,
                                                                         int maxAttempts,
                                                                         TimeSpan window);
    }
}
