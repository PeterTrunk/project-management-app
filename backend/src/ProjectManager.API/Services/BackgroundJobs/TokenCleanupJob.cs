using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProjectManager.API.Common.Options;
using ProjectManager.API.Data;

namespace ProjectManager.API.Services.BackgroundJobs
{
    /// <summary>
    /// A lejárt hitelesítési tokenek és az elévült feltöltés-naplósorok takarítása.
    ///
    /// Enélkül ezek a sorok örökre bent maradnak: a frissítő tokenek és a jelszó-visszaállító tokenek titkok,
    /// a logok pedig fájlneveket kötnek felhasználókhoz. A megőrzési idő betartása az adatkezelési tájékoztatóban vállalt kötelezettség. 
    /// </summary>
    public class TokenCleanupJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TokenCleanupJob> _logger;
        private readonly CleanupOptions _cleanupOptions;

        public TokenCleanupJob(
            IServiceProvider serviceProvider,
            ILogger<TokenCleanupJob> logger,
            IOptions<CleanupOptions> cleanupOptions)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _cleanupOptions = cleanupOptions.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromHours(_cleanupOptions.TokenCleanupIntervalHours));

            //A PeriodicTimer az első tickig végigvárja a teljes intervallumot, és minden
            //újraindítás után elölről kezdi - ezért induláskor egyszer azonnal is lefuttatjuk.
            await RunCleanupSafelyAsync(stoppingToken);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunCleanupSafelyAsync(stoppingToken);
            }
        }

        //A BackgroundServiceExceptionBehavior alapértelmezése StopHost:
        //egy kezeletlen kivétel innen a TELJES API-replikát leállítaná. Egy átmeneti PostgreSQL-hiba nem érhet ennyit.
        private async Task RunCleanupSafelyAsync(CancellationToken cancellationToken)
        {
            try
            {
                await CleanupAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token cleanup ciklus hiba");
            }
        }

        private async Task CleanupAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var now = DateTime.UtcNow;

            //Halmazműveletek: nincs értelme betölteni a sorokat, hogy aztán egyesével töröljük.
            //Az ExecuteDeleteAsync egyetlen DELETE utasítást küld, és nem érinti a change trackert.

            //A visszavontakat is a lejárat alapján visszük: a visszavonás ténye önmagában is
            //vizsgálati adat, amíg a token elvileg élhetne.
            var refreshCutoff = now.AddDays(-_cleanupOptions.RefreshTokenRetentionDays);
            var refreshDeleted = await context.RefreshTokens
                .Where(rt => rt.ExpiresAt < refreshCutoff)
                .ExecuteDeleteAsync(cancellationToken);

            //A felhasznált vagy lejárt visszaállító tokennek nincs másodlagos értéke, viszont
            //maga a Token mező titok - ezért haladék nélkül megy.
            var resetDeleted = await context.PasswordResetTokens
                .Where(t => t.IsUsed || t.ExpiresAt < now)
                .ExecuteDeleteAsync(cancellationToken);

            //Csak a megerősítettek: a meg nem erősítetteket az OrphanCleanupJob viszi el a
            //hozzájuk tartozó MinIO-fájllal együtt, jóval hamarabb.
            var uploadLogCutoff = now.AddDays(-_cleanupOptions.ConfirmedUploadLogRetentionDays);
            var uploadLogsDeleted = await context.PresignedUrlLogs
                .Where(p => p.Confirmed && p.CreatedAt < uploadLogCutoff)
                .ExecuteDeleteAsync(cancellationToken);

            if (refreshDeleted + resetDeleted + uploadLogsDeleted == 0)
                return;

            _logger.LogInformation(
                "Token cleanup | RefreshToken: {RefreshCount} | PasswordResetToken: {ResetCount} | PresignedUrlLog: {UploadLogCount}",
                refreshDeleted, resetDeleted, uploadLogsDeleted);
        }
    }
}
