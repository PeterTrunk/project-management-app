using Microsoft.Extensions.Logging.Abstractions;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.Services.GitBackfillService;
using System.Net;

namespace ProjectManager.Tests.Services.GitBackfill
{
    /// <summary>
    /// A GitLab compare végpontjáról való lekérés.
    ///
    /// Három dologban tér el a GitHubtól, és mindhárom saját tesztet kap: a végpont nem
    /// lapozható, hitelesítés nélkül 100 commitra vág, és a válasz maga mondja meg, ha az
    /// összehasonlítás időtúllépett. A saját telepítésű példány itt a tipikus eset.
    /// </summary>
    public class GitLabCommitFetcherTests
    {
        private const string Before = "1111111111111111111111111111111111111111";
        private const string After = "2222222222222222222222222222222222222222";

        private static (GitLabCommitFetcher Sut, StubHttpMessageHandler Handler) Create(
            StubHttpMessageHandler handler) =>
            (new GitLabCommitFetcher(
                new StubHttpClientFactory(handler),
                NullLogger<GitLabCommitFetcher>.Instance), handler);

        private static GitFetchRequest Request(
            string? instanceUrl = null, string? token = null, string repo = "peter/pma") =>
            new(repo, instanceUrl, token, Before, After);

        private static string Commit(int index) => $$"""
        {
          "id": "{{index:x40}}",
          "short_id": "{{index:x7}}",
          "title": "PMA-1 cím {{index}}",
          "message": "PMA-1 cím {{index}}\n\nA törzsben PMA-2 is szerepel.",
          "author_name": "Teszt Elek",
          "author_email": "teszt@example.com",
          "created_at": "2026-09-16T14:27:31+02:00",
          "web_url": "https://gitlab.com/peter/pma/-/commit/{{index:x7}}"
        }
        """;

        private static string Compare(int commitCount, bool timedOut = false) => $$"""
        {
          "commits": [ {{string.Join(",", Enumerable.Range(1, commitCount).Select(Commit))}} ],
          "compare_timeout": {{(timedOut ? "true" : "false")}},
          "compare_same_ref": false
        }
        """;

        [Fact]
        public void Provider_IsGitLab()
        {
            var (sut, _) = Create(StubHttpMessageHandler.Returning(Compare(0)));
            Assert.Equal(GitProviders.GitLab, sut.Provider);
        }

        [Fact]
        public async Task SuccessfulFetch_MapsEveryField()
        {
            var (sut, _) = Create(StubHttpMessageHandler.Returning(Compare(1)));

            var outcome = await sut.FetchRangeAsync(Request(token: "glpat-titok"));

            Assert.True(outcome.Success);
            Assert.False(outcome.Incomplete);

            var commit = Assert.Single(outcome.Commits);
            Assert.Equal("Teszt Elek", commit.AuthorName);
            Assert.Equal("teszt@example.com", commit.AuthorEmail);
            Assert.Equal("https://gitlab.com/peter/pma/-/commit/0000001", commit.Url);
            Assert.Equal(new DateTime(2026, 9, 16, 12, 27, 31, DateTimeKind.Utc), commit.CommittedAt);
        }

        /// <summary>
        /// Az üzenetnél a message az elsődleges, a title a tartalék. Nem mindegy: a feladatkulcs
        /// a commit üzenetének TÖRZSÉBEN is állhat, és a cím azt nem tartalmazza - a rövidebb
        /// mezőből illesztve a pótlás pont azokat a kapcsolatokat hagyná ki, amelyekért indult.
        /// </summary>
        [Fact]
        public async Task TheFullMessage_IsPreferredOverTheTitle()
        {
            var (sut, _) = Create(StubHttpMessageHandler.Returning(Compare(1)));

            var outcome = await sut.FetchRangeAsync(Request());

            Assert.Contains("PMA-2", Assert.Single(outcome.Commits).Message);
        }

        [Fact]
        public async Task WithoutAMessage_TheTitleIsUsed()
        {
            var payload = """
            {
              "commits": [ {
                "id": "aabbccddeeff00112233445566778899aabbccdd",
                "title": "PMA-1 csak cím",
                "author_name": "Teszt Elek",
                "created_at": "2026-09-16T14:27:31+02:00"
              } ],
              "compare_timeout": false
            }
            """;

            var (sut, _) = Create(StubHttpMessageHandler.Returning(payload));

            var outcome = await sut.FetchRangeAsync(Request());

            Assert.Equal("PMA-1 csak cím", Assert.Single(outcome.Commits).Message);
        }

        /// <summary>
        /// A straight=true a két állapot közvetlen összehasonlítása. Enélkül a szolgáltató a
        /// közös őstől számol, és a push során érkezett commitok helyett egy másik halmazt adna.
        /// </summary>
        [Fact]
        public async Task TheRequest_ComparesTheRangeDirectly()
        {
            var (sut, handler) = Create(StubHttpMessageHandler.Returning(Compare(1)));

            await sut.FetchRangeAsync(Request());

            var request = Assert.Single(handler.Requests);
            Assert.Contains("https://gitlab.com/api/v4/projects/", request.Url);
            Assert.Contains($"from={Before}", request.Url);
            Assert.Contains($"to={After}", request.Url);
            Assert.Contains("straight=true", request.Url);
        }

        /// <summary>
        /// A tárház azonosítója útvonalrészként utazik, a GitLab viszont enged alcsoportot is
        /// (csoport/alcsoport/projekt). Kódolás nélkül a perjelek más végpontra mutatnának.
        /// </summary>
        [Fact]
        public async Task ASubgroupPath_IsUrlEncoded()
        {
            var (sut, handler) = Create(StubHttpMessageHandler.Returning(Compare(1)));

            await sut.FetchRangeAsync(Request(repo: "ceg/csapat/pma"));

            var request = Assert.Single(handler.Requests);
            Assert.Contains("/projects/ceg%2Fcsapat%2Fpma/repository/compare", request.Url);
        }

        //A személyes hozzáférési token saját fejlécben utazik, nem Bearerként
        [Fact]
        public async Task WithAToken_SendsThePrivateTokenHeader()
        {
            var (sut, handler) = Create(StubHttpMessageHandler.Returning(Compare(1)));

            await sut.FetchRangeAsync(Request(token: "glpat-titok"));

            var request = Assert.Single(handler.Requests);
            Assert.Equal("glpat-titok", request.Header("PRIVATE-TOKEN"));
            Assert.Null(request.Header("Authorization"));
        }

        [Fact]
        public async Task SelfHosted_AddsTheApiPathToTheGivenServer()
        {
            var (sut, handler) = Create(StubHttpMessageHandler.Returning(Compare(1)));

            await sut.FetchRangeAsync(Request(instanceUrl: "https://git.ceg.hu"));

            Assert.StartsWith(
                "https://git.ceg.hu/api/v4/projects/peter%2Fpma/repository/compare",
                Assert.Single(handler.Requests).Url);
        }

        //A szolgáltató maga mondja meg, ha az összehasonlítás nem ért végig
        [Fact]
        public async Task AProviderSideTimeout_IsReportedAsIncomplete()
        {
            var (sut, _) = Create(StubHttpMessageHandler.Returning(Compare(5, timedOut: true)));

            var outcome = await sut.FetchRangeAsync(Request(token: "glpat-titok"));

            Assert.True(outcome.Success);
            Assert.True(outcome.Incomplete);
            Assert.Equal(5, outcome.Commits.Count);
        }

        /// <summary>
        /// Token nélkül a szolgáltató 100 commitra vágja a listát. Pontosan ennyinél tehát nem
        /// tudjuk, vége van-e az intervallumnak - és a „nem tudjuk" itt nem hallgatható el:
        /// ez ugyanaz a néma csonkolás, amely ellen az egész funkció készül.
        /// </summary>
        [Fact]
        public async Task ExactlyTheUnauthenticatedLimit_WithoutAToken_IsIncomplete()
        {
            var (sut, _) = Create(StubHttpMessageHandler.Returning(Compare(100)));

            var outcome = await sut.FetchRangeAsync(Request());

            Assert.True(outcome.Incomplete);
        }

        [Fact]
        public async Task ExactlyTheUnauthenticatedLimit_WithAToken_IsComplete()
        {
            var (sut, _) = Create(StubHttpMessageHandler.Returning(Compare(100)));

            var outcome = await sut.FetchRangeAsync(Request(token: "glpat-titok"));

            Assert.False(outcome.Incomplete);
        }

        [Fact]
        public async Task TheEndpointIsNotPaged_SoOneRequestIsEnough()
        {
            var (sut, handler) = Create(StubHttpMessageHandler.Returning(Compare(100)));

            await sut.FetchRangeAsync(Request(token: "glpat-titok"));

            Assert.Single(handler.Requests);
        }

        //Hibaágak

        [Fact]
        public async Task AnErrorResponse_IsClassified()
        {
            var (sut, _) = Create(StubHttpMessageHandler.Returning("{}", HttpStatusCode.NotFound));

            var outcome = await sut.FetchRangeAsync(Request());

            Assert.False(outcome.Success);
            Assert.Equal(GitFetchErrorKind.NotFound, outcome.ErrorKind);
        }

        /// <summary>
        /// Saját telepítésnél ez a leggyakoribb hiba: elírt cím, vagy a kiszolgáló a mi
        /// hálózatunkból nem érhető el. A hibaszöveg ezért nevezi meg a kiszolgáló címét mint
        /// ellenőrizendő dolgot - a felhasználó enélkül a tokent kezdené cserélni.
        /// </summary>
        [Fact]
        public async Task AnUnreachableSelfHostedServer_PointsAtTheAddress()
        {
            var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("ismeretlen hoszt"));
            var (sut, _) = Create(handler);

            var outcome = await sut.FetchRangeAsync(Request(instanceUrl: "https://elirt.ceg.hu"));

            Assert.Equal(GitFetchErrorKind.Transient, outcome.ErrorKind);
            Assert.Contains("kiszolgáló címét", outcome.ErrorMessage);
        }

        [Fact]
        public async Task AResponseWithoutACommitArray_IsAPermanentFailure()
        {
            var (sut, _) = Create(StubHttpMessageHandler.Returning("""{ "compare_same_ref": true }"""));

            var outcome = await sut.FetchRangeAsync(Request());

            Assert.Equal(GitFetchErrorKind.Permanent, outcome.ErrorKind);
        }
    }
}
