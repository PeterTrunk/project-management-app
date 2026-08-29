using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProjectManager.API.Common.Options;
using ProjectManager.API.Data;
using ProjectManager.API.Services.FileStorageService;

namespace ProjectManager.API.Services.BackgroundJobs
{
    public class OrphanCleanupJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OrphanCleanupJob> _logger;
        private readonly CleanupOptions _cleanupOptions;

        public OrphanCleanupJob(
            IServiceProvider serviceProvider, 
            ILogger<OrphanCleanupJob> logger,
            IOptions<CleanupOptions> cleanupOptions)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _cleanupOptions = cleanupOptions.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromHours(_cleanupOptions.OrphanCleanupIntervalHours));

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
