namespace ProjectManager.API.Common.Constants
{
    /// <summary>
    /// A commit pótlás (backfill) állapotai. Ez az érték kerül a CommitBackfill.State oszlopba,
    /// tehát adatbázisban tárolt szöveg: átírásához migráció kell, nem elég a konstans módosítása.
    ///
    /// A Failed NEM végállapot: a foglalás feloldódik, és a sor újraindítható. Ezért nincs
    /// külön "Abandoned" érték - a pótlás hiánya nem rosszabb annál, mint amikor a hiányt
    /// észleljük, de még senki nem nyúlt hozzá.
    /// </summary>
    public static class BackfillStates
    {
        /// <summary>Észleltük a hiányt, a pótlás még nem indult el.</summary>
        public const string Pending = "Pending";

        /// <summary>Valaki (vagy a rendszer) lefoglalta a sort, a lekérés fut.</summary>
        public const string InProgress = "InProgress";

        /// <summary>A hiányzó commitokat behúztuk.</summary>
        public const string Completed = "Completed";

        /// <summary>A lekérés hibára futott. Újraindítható, a hibaok a LastError mezőben.</summary>
        public const string Failed = "Failed";

        public static readonly string[] All = [Pending, InProgress, Completed, Failed];
    }
}
