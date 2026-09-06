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

            //A PeriodicTimer az első tickig végigvárja a teljes intervallumot, és minden újraindítás után elölről kezdi.
            //Enélkül egy 24 órás ciklusnál a megerősítés nélkül feltöltött fájlok sokáig a tárolóban maradnának.
            await RunCleanupSafelyAsync();

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunCleanupSafelyAsync();
            }
        }

        //A BackgroundServiceExceptionBehavior alapértelmezése StopHost: egy kezeletlen kivétel innen a TELJES API-replikát leállítaná.
        //Egy átmeneti PostgreSQL-hiba nem állítja le a szolgáltatást.
        private async Task RunCleanupSafelyAsync()
        {
            try
            {
                await CleanupOrphansAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Orphan cleanup ciklus hiba");
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
                    //Külön transaction-ökben egyenkénti törlések.
                    await fileStorage.DeleteFileAsync(orphan.StorageKey);
                    context.PresignedUrlLogs.Remove(orphan);
                    await context.SaveChangesAsync();
                    _logger.LogInformation("Orphan törölve: {StorageKey}", orphan.StorageKey);
                }
                catch (DbUpdateConcurrencyException)
                {
                    //Több replika esetén, ha párhuzamosan futtatták akkor nem csak skip
                    _logger.LogInformation("Orphan már törölve másik replika által: {StorageKey}", orphan.StorageKey);
                    context.ChangeTracker.Clear();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Orphan törlési hiba: {StorageKey}", orphan.StorageKey);
                    context.ChangeTracker.Clear();
                }
            }
        }
    }
}
