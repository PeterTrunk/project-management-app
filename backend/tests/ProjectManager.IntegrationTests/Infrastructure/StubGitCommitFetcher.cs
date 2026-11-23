using ProjectManager.API.Common.Constants;
using ProjectManager.API.Services.GitBackfillService;

namespace ProjectManager.IntegrationTests.Infrastructure
{
    /// <summary>
    /// A szolgáltatói commit-lekérés dupla.
    ///
    /// Ez az egyik hely, ahol a dupla elkerülhetetlen: a valódi lekérő a GitHubot vagy a
    /// GitLabot kérdezné, ami hálózatot és kvótát igényel, a hibaágak (lejárt token, kifogyott
    /// kvóta, időtúllépés) pedig élesben szándékosan nem előállíthatók. Magát a lekérőt a
    /// ProjectManager.Tests fedi le, kézi HTTP válaszadóval.
    ///
    /// Itt az a kérdés, amit a dupla jól mér: mit tesz a pótlás a kapott válasszal - lefoglalja-e
    /// a sort, átvezeti-e a commitokat az illesztőn, és mi lesz a nyilvántartási sor állapota.
    /// </summary>
    public sealed class StubGitCommitFetcher : IGitCommitFetcher
    {
        public string Provider { get; init; } = GitProviders.GitHub;

        /// <summary>Amit minden hívásra ad, ha nincs <see cref="Respond"/>.</summary>
        public GitFetchOutcome Outcome { get; set; } = GitFetchOutcome.Ok([]);

        /// <summary>Hívásonként más válasz, például versenyhelyzet vizsgálatához.</summary>
        public Func<GitFetchRequest, GitFetchOutcome>? Respond { get; set; }

        /// <summary>A kapott kérések, hívási sorrendben: ebből látszik, mit vittünk ki.</summary>
        public List<GitFetchRequest> Requests { get; } = [];

        public int Calls => Requests.Count;

        public Task<GitFetchOutcome> FetchRangeAsync(
            GitFetchRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);

            return Task.FromResult(Respond?.Invoke(request) ?? Outcome);
        }
    }
}
