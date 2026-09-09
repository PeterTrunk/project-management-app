namespace ProjectManager.API.Common.Options
{
    public class CleanupOptions
    {
        public int OrphanCleanupIntervalHours { get; set; } = 24;

        /// <summary>Milyen gyakran fusson a lejárt tokenek és naplósorok takarítása.</summary>
        public int TokenCleanupIntervalHours { get; set; } = 6;

        /// <summary>
        /// Lejárat után ennyi napig maradnak meg a frissítő tokenek. Nem nulla: egy ellopott
        /// token utólagos vizsgálatához tudni kell, mikor és meddig élt.
        /// </summary>
        public int RefreshTokenRetentionDays { get; set; } = 30;

        /// <summary>
        /// A megerősített feltöltés-naplósorok megőrzési ideje. A meg nem erősítetteket az
        /// OrphanCleanupJob viszi el jóval hamarabb, a fájllal együtt.
        /// </summary>
        public int ConfirmedUploadLogRetentionDays { get; set; } = 90;
    }
}
