namespace ProjectManager.API.Services.GitWebhookService.Payloads
{
    /// <summary>
    /// A pull request / merge request eseményből az, amire reagálunk. 
    /// A szolgáltatók saját szókincsét a parser fordítja.
    /// (GitHub: opened/closed/reopened/edited, GitLab: open/close/reopen/merge/update)
    /// Ami egyikbe sem esik, azt a parser eleve elutasítja, tehát ide nem jut el.
    /// </summary>
    public enum GitPullRequestAction
    {
        Opened,
        Reopened,
        Closed,
        Edited
    }

    /// <summary>
    /// A PrLink.State oszlopban tárolt értékek. Adatbázisban lévő szöveg.
    /// A frontend is ezek alapján színez, átírásuk migrációt igényelne.
    /// </summary>
    public static class GitPrStates
    {
        public const string Open = "open";
        public const string Closed = "closed";
        public const string Merged = "merged";
    }

    /// <summary>
    /// Egy pull request / merge request esemény normalizált alakja.
    /// </summary>
    /// <param name="Number">A sorszám, ahogy a felhasználó látja (GitHub: number, GitLab: iid).</param>
    /// <param name="Title">A PR címe.</param>
    /// <param name="Description">A PR leírása (GitHub: body, GitLab: description).</param>
    /// <param name="Url">A PR webes címe, ha a szolgáltató küldte.</param>
    /// <param name="AuthorName">A megjelenítendő szerzőnév.</param>
    /// <param name="Action">Mi történt a PR-rel.</param>
    /// <param name="State">Az eredő állapot: <see cref="GitPrStates"/> egyik értéke.</param>
    /// <param name="MergedAt">A merge ideje UTC-ben, ha az állapot merged.</param>
    public sealed record GitPullRequestEvent(
        int Number,
        string Title,
        string? Description,
        string? Url,
        string AuthorName,
        GitPullRequestAction Action,
        string State,
        DateTime? MergedAt);
}
