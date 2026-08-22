using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Data;
using ProjectManager.API.Services.FileStorageService;

namespace ProjectManager.API.Services.BackgroundJobs
{
    public class OrphanCleanupJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OrphanCleanupJob> _logger;

        public OrphanCleanupJob(IServiceProvider serviceProvider, ILogger<OrphanCleanupJob> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var intervalHours = int.Parse(
                Environment.GetEnvironmentVariable("ORPHAN_CLEANUP_INTERVAL_HOURS") ?? "24");

            using var timer = new PeriodicTimer(TimeSpan.FromHours(intervalHours));

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await CleanupOrphansAsync();
            }
        }

        private async Task CleanupOrphansAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var fileStorage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();

            var cutoff = DateTime.UtcNow.AddMinutes(-15);

            var orphans = await context.PresignedUrlLogs
                .Where(p => !p.Confirmed && p.ExpiresAt < cutoff)
                .ToListAsync();

            _logger.LogInformation("Orphan cleanup: {Count} fájl törlése", orphans.Count);

            foreach (var orphan in orphans)
            {
                try
                {
                    await fileStorage.DeleteFileAsync(orphan.StorageKey);
                    context.PresignedUrlLogs.Remove(orphan);
                    _logger.LogInformation("Orphan törölve: {StorageKey}", orphan.StorageKey);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Orphan törlési hiba: {StorageKey}", orphan.StorageKey);
                }
            }

            await context.SaveChangesAsync();
        }
    }
}
