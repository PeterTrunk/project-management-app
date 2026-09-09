namespace ProjectManager.API.Model
{
    /// <summary>
    /// Annak bizonyítéka, hogy egy felhasználó mikor és melyik dokumentumverziót fogadta el.
    ///
    /// Külön táblában és nem a User két oszlopában, mert egy felhasználó idővel több verziót is elfogadhat, 
    /// és a GDPR 7. cikk (1) szerinti elszámoltathatósághoz a TÖRTÉNETRE van szükség,
    /// nem csak az utolsó állapotra. Így a későbbi újraelfogadtató folyamat séma-változtatás nélkül ráépül.
    /// </summary>
    public class UserTermsAcceptance
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid TermsVersionId { get; set; }

        /// <summary>
        /// Szándékosan nem CreatedAt: ez jogi tény, ezért kifejezetten állítjuk be,
        /// nem az AppDbContext automatikus időbélyegzésére bízzuk.
        /// </summary>
        public DateTime AcceptedAt { get; set; }

        public User User { get; set; } = null!;
        public TermsVersion TermsVersion { get; set; } = null!;
    }
}
