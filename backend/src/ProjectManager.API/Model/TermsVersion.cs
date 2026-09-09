namespace ProjectManager.API.Model
{
    /// <summary>
    /// A jogi dokumentumok egy közzétett változata. A sorok után nem nyúlunk: ha a szöveg érdemben változik, ÚJ sor keletkezik.
    /// Enélkül a korábbi elfogadások olyan tartalomra hivatkoznának, ami már nem az, amit a felhasználó látott.
    ///
    /// Az adatkezelési tájékoztató és a felhasználási feltételek szándékosan KÖZÖS verziót kapnak: 
    /// A felületen egyetlen jelölőnégyzet vonatkozik mindkettőre, tehát a külön verziózás olyan függetlenséget sugallna,
    /// ami a felhasználói élményben nem létezik.
    /// </summary>
    public class TermsVersion
    {
        public Guid Id { get; set; }

        /// <summary>Dátum alapú azonosító, pl. "2026-09-09".</summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Ettől a pillanattól hatályos. A hatályos verzió mindig a legkésőbbi olyan sor, amelynek ez az értéke már elmúlt, 
        /// így nincs külön "aktív" jelző, ami elavulhatna, és egy jövőbeli verzió előre felvehető.
        /// </summary>
        public DateTime EffectiveFrom { get; set; }

        public DateTime CreatedAt { get; set; }

        public ICollection<UserTermsAcceptance> Acceptances { get; set; } = new List<UserTermsAcceptance>();
    }
}
