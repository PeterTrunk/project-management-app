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
    /// nincs naplózója, a hívó viszont ebből tud figyelmeztetést írni.</param>
    public sealed record GitPushEvent(
        IReadOnlyList<GitCommitInfo> Commits,
        int SkippedCommitCount);
}
