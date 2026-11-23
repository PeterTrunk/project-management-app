using ProjectManager.API.Services.GitWebhookService.Payloads;

namespace ProjectManager.API.Services.GitBackfillService
{
    /// <summary>
    /// Egy pótló lekérés paraméterei.
    ///
    /// A hívó adja át a visszafejtett tokent: a titkosítás az integráció tárolásának a kérdése,
    /// nem a lekérésnek. Így a fetcher Docker és adatbázis nélkül tesztelhető, és a titok
    /// visszafejtése egyetlen helyen marad.
    /// </summary>
    /// <param name="RepoFullName">A tárház azonosítója a szolgáltatónál (tulajdonos/tárház).</param>
    /// <param name="InstanceUrl">Saját telepítésű szolgáltató címe, vagy null a felhő esetén.</param>
    /// <param name="AccessToken">A hozzáférési token, vagy null: publikus tárházból anélkül is olvasható.</param>
    /// <param name="BeforeSha">A push előtti állapot - a lekért intervallum kezdete (nem tartozik bele).</param>
    /// <param name="AfterSha">A push utáni állapot - a lekért intervallum vége.</param>
    public sealed record GitFetchRequest(
        string RepoFullName,
        string? InstanceUrl,
        string? AccessToken,
        string BeforeSha,
        string AfterSha);

    /// <summary>
    /// A hiba fajtája. Azért osztályozzuk, mert három különböző válasz tartozik hozzájuk:
    /// újrapróbálkozás, hitelesítő adat cserélése, vagy a pótlás feladása.
    /// </summary>
    public enum GitFetchErrorKind
    {
        /// <summary>
        /// A hitelesítő adat a hibás vagy hiányzó. Ez az az eset, amely BIZTOSAN nem oldódik
        /// meg magától: amíg valaki nem cserél tokent, minden további kísérlet ugyanide fut.
        /// </summary>
        Authentication,

        /// <summary>
        /// A szolgáltató nem találja a tárházat vagy az intervallumot: átnevezték, törölték,
        /// vagy force push után a régi azonosító már nem létezik. Privát tárháznál a hiányzó
        /// jogosultság IS ide vezet, mert a GitHub nem felfedi a privát tárházak létét.
        /// </summary>
        NotFound,

        /// <summary>Átmeneti: a szolgáltató nem elérhető, időtúllépés, vagy elfogyott a kvóta.</summary>
        Transient,

        /// <summary>Minden más: a kérés alakja vagy a válasz értelmezhetetlen.</summary>
        Permanent
    }

    /// <summary>
    /// Egy pótló lekérés eredménye. Kivétel helyett érték, mert a hívónak döntenie kell a hiba
    /// fajtája alapján, és ugyanez a besorolás kerül a nyilvántartási sorba és a naplóba is.
    /// </summary>
    public sealed record GitFetchOutcome
    {
        public bool Success { get; private init; }

        public IReadOnlyList<GitCommitInfo> Commits { get; private init; } = [];

        /// <summary>
        /// Igaz, ha a lekérés sem adta meg a teljes intervallumot: a lapozás a felső határba
        /// futott, a szolgáltató oldali összehasonlítás időtúllépett, vagy token nélkül
        /// vágott a szolgáltató. Nem hiba, de NEM is hallgatható el - különben a pótlás
        /// ugyanazt a néma csonkolást termelné újra, amit megszüntetni hivatott.
        /// </summary>
        public bool Incomplete { get; private init; }

        public GitFetchErrorKind? ErrorKind { get; private init; }

        /// <summary>
        /// Rövid, SAJÁT megfogalmazású hibaok. A szolgáltató nyers válasza szándékosan nem
        /// kerül ide: tárházneveket és egyéb, naplóba nem való részleteket hordozhat, és ez a
        /// szöveg a felületre is kimegy.
        /// </summary>
        public string? ErrorMessage { get; private init; }

        public static GitFetchOutcome Ok(IReadOnlyList<GitCommitInfo> commits, bool incomplete = false) =>
            new() { Success = true, Commits = commits, Incomplete = incomplete };

        public static GitFetchOutcome Fail(GitFetchErrorKind kind, string message) =>
            new() { Success = false, ErrorKind = kind, ErrorMessage = message };
    }
}
