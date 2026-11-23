using ProjectManager.API.Services.GitWebhookService.Payloads;

namespace ProjectManager.API.Services.GitBackfillService
{
    /// <summary>
    /// Commitok lekérése a git szolgáltató API-járól.
    ///
    /// Ez az első kimenő irány a rendszerben: eddig a git integráció csak FOGADOTT. Azért kell,
    /// mert a webhook payload nem feltétlenül a teljes push - a szolgáltatók levágják a commit
    /// tömböt (GitLab 20, GitHub 2048) -, és a kimaradt commitok csak az API-n kérhetők le.
    ///
    /// A réteg a payload parserek mintáját követi: providerenként egy megvalósítás, a hívó a
    /// Provider alapján választ. A visszatérés a webhookéval AZONOS <see cref="GitCommitInfo"/>,
    /// tehát a beillesztő oldal nem tud róla, honnan jött az adat - ez teszi lehetővé, hogy a
    /// pótlás ugyanazon az illesztő és naplózó úton menjen át.
    /// </summary>
    public interface IGitCommitFetcher
    {
        /// <summary>Melyik Integration.Provider értékhez tartozik. Lásd GitProviders.</summary>
        string Provider { get; }

        /// <summary>
        /// A két állapot közötti commitok. Kivételt nem dob: a hiba az eredmény része, mert a
        /// hívónak osztályoznia kell (újrapróbálható-e), és a nyilvántartási sorba is a
        /// besorolás kerül, nem egy kivétel szövege.
        /// </summary>
        Task<GitFetchOutcome> FetchRangeAsync(GitFetchRequest request, CancellationToken cancellationToken = default);
    }
}
