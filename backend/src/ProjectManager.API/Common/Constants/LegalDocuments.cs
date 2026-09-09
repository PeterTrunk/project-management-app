namespace ProjectManager.API.Common.Constants
{
    /// <summary>
    /// A közzétett jogi dokumentumok (adatkezelési tájékoztató és felhasználási feltételek)
    /// hatályos verziója.
    /// </summary>
    public static class LegalDocuments
    {
        /// <summary>
        /// A hatályos verzió azonosítója.
        ///
        /// FONTOS: ennek meg KELL egyeznie a frontend <c>src/lib/legal.ts</c> fájljában lévő
        /// LEGAL_VERSION értékkel. A kliens a regisztrációkor visszaküldi az általa
        /// megjelenített verziót, és a szerver csak akkor fogadja el, ha az a hatályos -
        /// így egy régi, gyorsítótárazott felület nem tud egy már nem látható szövegre
        /// hivatkozó elfogadást rögzíteni.
        /// </summary>
        public const string CurrentVersion = "2026-09-09";

        /// <summary>A hatályos verzió hatálybalépésének napja (UTC).</summary>
        public static readonly DateTime CurrentVersionEffectiveFrom =
            new(2026, 9, 9, 0, 0, 0, DateTimeKind.Utc);
    }
}
