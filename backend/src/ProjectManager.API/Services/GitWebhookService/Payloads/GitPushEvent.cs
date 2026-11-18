namespace ProjectManager.API.Services.GitWebhookService.Payloads
{
    /// <summary>
    /// Egy commit a push eseményből, szolgáltatótól függetlenül.
    /// </summary>
    /// <param name="Sha">A commit teljes azonosítója.</param>
    /// <param name="Message">A commit üzenete. Ebben keressük a task kulcsokat.</param>
    /// <param name="Url">A commit webes címe, ha a szolgáltató küldte.</param>
    /// <param name="AuthorName">A szerző neve a commit metaadatából - NEM a pusholó felhasználó.</param>
    /// <param name="AuthorEmail">A szerző e-mail címe. Nem kerül ki a felületre, lásd CommitLink.</param>
    /// <param name="CommittedAt">A commit ideje UTC-ben. Hiányzó vagy értelmezhetetlen időbélyegnél a feldolgozás ideje.</param>
    public sealed record GitCommitInfo(
        string Sha,
        string Message,
        string? Url,
        string AuthorName,
        string AuthorEmail,
        DateTime CommittedAt);

    /// <summary>
    /// Egy push esemény normalizált alakja.
    /// </summary>
    /// <param name="Commits">A feldolgozható commitok. Üres lista is érvényes: a GitLab ág törlésekor
    /// üres tömböt küld.</param>
    /// <param name="SkippedCommitCount">Hány commitot hagytunk ki hiányzó kötelező mező miatt.
    /// Azért utazik a rekordban, mert enélkül a kihagyás NÉMA lenne: a parser tiszta függvény,
    /// nincs naplózója, a hívó viszont ebből tud figyelmeztetést írni.
    ///
    /// Ez NEM a szolgáltatói csonkolás: itt a MI parserünk hagyott ki valamit, ott a szolgáltató
    /// küldött kevesebbet. Lásd CommitsPossiblyTruncated.</param>
    /// <param name="ProviderReportedCommitCount">Amennyi commitot a szolgáltató a pushra ígért.
    /// GitLabnál a total_commits_count; a GitHub nem küld ilyen mezőt, ott null marad.</param>
    /// <param name="CommitsPossiblyTruncated">Igaz, ha a payload commit tömbje feltehetően nem
    /// a teljes push. A döntés szolgáltatófüggő, ezért a parser hozza meg: GitLabnál BIZTOS
    /// (az ígért darabszám több a kapottnál), GitHubnál csak VALÓSZÍNŰ (a tömb pontosan a felső
    /// határon áll, és a szolgáltató nem közöl darabszámot).</param>
    /// <param name="BeforeSha">A push előtti állapot azonosítója - a pótló hívás paramétere.</param>
    /// <param name="AfterSha">A push utáni állapot azonosítója - a pótló hívás paramétere.</param>
    /// <param name="Ref">Az ág, amelyre a push érkezett (pl. refs/heads/main).</param>
    public sealed record GitPushEvent(
        IReadOnlyList<GitCommitInfo> Commits,
        int SkippedCommitCount,
        int? ProviderReportedCommitCount = null,
        bool CommitsPossiblyTruncated = false,
        string? BeforeSha = null,
        string? AfterSha = null,
        string? Ref = null)
    {
        /// <summary>
        /// Amennyi commit ténylegesen megérkezett a payloadban. Az értelmezhetetlenek IS
        /// beleszámítanak: azok megjöttek, csak mi nem tudtuk elolvasni őket, tehát a
        /// szolgáltatói csonkolás mértékét nem torzíthatják.
        /// </summary>
        public int ReceivedCommitCount => Commits.Count + SkippedCommitCount;
    }
}
