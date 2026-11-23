using Microsoft.Extensions.Logging.Abstractions;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.Services.GitBackfillService;
using System.Net;

namespace ProjectManager.Tests.Services.GitBackfill
{
    /// <summary>
    /// A GitHub compare végpontjáról való lekérés.
    ///
    /// A végpont alakja NEM azonos a webhook payloadjával (a commit metaadata beágyazott
    /// objektumban van, a sha pedig „sha" és nem „id"), ezért van saját olvasása - és ezért
    /// kell tesztelni, hogy ugyanazt a normalizált alakot adja, mint a webhook ága.
    /// </summary>
    public class GitHubCommitFetcherTests
    {
        private const string Before = "1111111111111111111111111111111111111111";
        private const string After = "2222222222222222222222222222222222222222";

        private static (GitHubCommitFetcher Sut, StubHttpMessageHandler Handler) Create(
            StubHttpMessageHandler handler) =>
            (new GitHubCommitFetcher(
                new StubHttpClientFactory(handler),
                NullLogger<GitHubCommitFetcher>.Instance), handler);

        private static GitFetchRequest Request(string? instanceUrl = null, string? token = null) =>
            new("peter/pma", instanceUrl, token, Before, After);

        private static string Commit(int index) => $$"""
        {
          "sha": "{{index:x40}}",
          "html_url": "https://github.com/peter/pma/commit/{{index:x7}}",
          "commit": {
            "message": "PMA-1 commit {{index}}",
            "author": {
              "name": "Teszt Elek",
              "email": "teszt@example.com",
              "date": "2026-09-16T14:27:31+02:00"
            }
          }
        }
        """;

        private static string Page(int commitCount) =>
            $$"""{ "commits": [ {{string.Join(",", Enumerable.Range(1, commitCount).Select(Commit))}} ] }""";

        [Fact]
        public void Provider_IsGitHub()
        {
            var (sut, _) = Create(StubHttpMessageHandler.Returning(Page(0)));
            Assert.Equal(GitProviders.GitHub, sut.Provider);
        }

        [Fact]
        public async Task SuccessfulFetch_MapsEveryField()
        {
            var (sut, _) = Create(StubHttpMessageHandler.Returning(Page(1)));

            var outcome = await sut.FetchRangeAsync(Request());

            Assert.True(outcome.Success);
            Assert.False(outcome.Incomplete);

            var commit = Assert.Single(outcome.Commits);
            Assert.Equal("PMA-1 commit 1", commit.Message);
            Assert.Equal("Teszt Elek", commit.AuthorName);
            Assert.Equal("teszt@example.com", commit.AuthorEmail);
            Assert.StartsWith("https://github.com/", commit.Url);

            //Az eltolásos időbélyeg UTC-ben, ugyanúgy, mint a webhook ágán
            Assert.Equal(DateTimeKind.Utc, commit.CommittedAt.Kind);
            Assert.Equal(new DateTime(2026, 9, 16, 12, 27, 31, DateTimeKind.Utc), commit.CommittedAt);
        }

        [Fact]
        public async Task TheRequest_AddressesTheCompareEndpointWithPaging()
        {
            var (sut, handler) = Create(StubHttpMessageHandler.Returning(Page(1)));

            await sut.FetchRangeAsync(Request());

            var request = Assert.Single(handler.Requests);
            Assert.Contains("https://api.github.com/repos/peter/pma/compare/", request.Url);
            Assert.Contains($"{Before}...{After}", request.Url);
            Assert.Contains("per_page=100", request.Url);
            Assert.Contains("page=1", request.Url);
        }

        [Fact]
        public async Task WithAToken_SendsItAsABearerCredential()
        {
            var (sut, handler) = Create(StubHttpMessageHandler.Returning(Page(1)));

            await sut.FetchRangeAsync(Request(token: "ghp_titok"));

            Assert.Equal("Bearer ghp_titok", Assert.Single(handler.Requests).Header("Authorization"));
        }

        /// <summary>
        /// Token nélkül is van értelme a hívásnak: publikus tárházból olvasható a commitlista,
        /// csak szűkebb kérésszám-korláttal. Ezért a hitelesítő fejléc hiánya nem hiba.
        /// </summary>
        [Fact]
        public async Task WithoutAToken_SendsNoCredential()
        {
            var (sut, handler) = Create(StubHttpMessageHandler.Returning(Page(1)));

            await sut.FetchRangeAsync(Request());

            Assert.Null(Assert.Single(handler.Requests).Header("Authorization"));
        }

        [Fact]
        public async Task TheCloud_GetsAPinnedApiVersion()
        {
            var (sut, handler) = Create(StubHttpMessageHandler.Returning(Page(1)));

            await sut.FetchRangeAsync(Request());

            Assert.NotNull(Assert.Single(handler.Requests).Header("X-GitHub-Api-Version"));
        }

        /// <summary>
        /// Saját telepítésű Enterprise Servernél a verziót NEM mondjuk ki: az a példány a maga
        /// kiadásához tartozó verziókat ismeri, és egy általa nem ismert verziószámtól minden
        /// kérésünk elutasításra kerülne - vagyis a pótlás pont a saját telepítésnél, ahol a
        /// legnagyobb szükség van rá, állna le.
        /// </summary>
        [Fact]
        public async Task SelfHosted_SendsNoApiVersionAndUsesTheEnterprisePath()
        {
            var (sut, handler) = Create(StubHttpMessageHandler.Returning(Page(1)));

            await sut.FetchRangeAsync(Request(instanceUrl: "https://ghe.ceg.hu"));

            var request = Assert.Single(handler.Requests);
            Assert.StartsWith("https://ghe.ceg.hu/api/v3/repos/peter/pma/compare/", request.Url);
            Assert.Null(request.Header("X-GitHub-Api-Version"));
        }

        //Lapozás: a rövidebb lap jelzi az intervallum végét

        [Fact]
        public async Task AFullPage_IsFollowedByTheNext()
        {
            var (sut, handler) = Create(StubHttpMessageHandler.Paging(page => Page(page == 1 ? 100 : 30)));

            var outcome = await sut.FetchRangeAsync(Request());

            Assert.Equal(130, outcome.Commits.Count);
            Assert.False(outcome.Incomplete);
            Assert.Equal(2, handler.Requests.Count);
            Assert.Contains("page=2", handler.Requests[1].Url);
        }

        [Fact]
        public async Task AnEmptyFollowingPage_EndsTheWalk()
        {
            var (sut, handler) = Create(StubHttpMessageHandler.Paging(page => Page(page == 1 ? 100 : 0)));

            var outcome = await sut.FetchRangeAsync(Request());

            Assert.Equal(100, outcome.Commits.Count);
            Assert.False(outcome.Incomplete);
            Assert.Equal(2, handler.Requests.Count);
        }

        /// <summary>
        /// A lapozásnak felső határa van, különben egy nagy intervallum korlátlan számú kimenő
        /// hívást indítana, felhasználói kérésben. A határba futást viszont jelezni KELL:
        /// különben a pótlás ugyanazt a néma csonkolást termelné újra, amit megszüntetni hivatott.
        /// </summary>
        [Fact]
        public async Task HittingThePageLimit_IsReportedAsIncomplete()
        {
            var (sut, handler) = Create(StubHttpMessageHandler.Paging(_ => Page(100)));

            var outcome = await sut.FetchRangeAsync(Request());

            Assert.True(outcome.Success);
            Assert.True(outcome.Incomplete);
            Assert.Equal(1000, outcome.Commits.Count);
            Assert.Equal(10, handler.Requests.Count);
        }

        //Hibaágak

        [Fact]
        public async Task AnErrorResponse_IsClassifiedAndStopsTheWalk()
        {
            var (sut, handler) = Create(StubHttpMessageHandler.Returning("{}", HttpStatusCode.Unauthorized));

            var outcome = await sut.FetchRangeAsync(Request(token: "lejart"));

            Assert.False(outcome.Success);
            Assert.Equal(GitFetchErrorKind.Authentication, outcome.ErrorKind);
            Assert.Single(handler.Requests);
        }

        [Fact]
        public async Task AResponseWithoutACommitArray_IsAPermanentFailure()
        {
            var (sut, _) = Create(StubHttpMessageHandler.Returning("""{ "status": "ahead" }"""));

            var outcome = await sut.FetchRangeAsync(Request());

            Assert.False(outcome.Success);
            Assert.Equal(GitFetchErrorKind.Permanent, outcome.ErrorKind);
        }

        [Fact]
        public async Task AnUnparseableResponse_IsAPermanentFailure()
        {
            var (sut, _) = Create(StubHttpMessageHandler.Returning("nem json"));

            var outcome = await sut.FetchRangeAsync(Request());

            Assert.False(outcome.Success);
            Assert.Equal(GitFetchErrorKind.Permanent, outcome.ErrorKind);
        }

        [Fact]
        public async Task AnUnreachableProvider_IsTransient()
        {
            var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("nincs hálózat"));
            var (sut, _) = Create(handler);

            var outcome = await sut.FetchRangeAsync(Request());

            Assert.False(outcome.Success);
            Assert.Equal(GitFetchErrorKind.Transient, outcome.ErrorKind);
        }

        /// <summary>
        /// Az időtúllépést a HttpClient megszakításként jelzi. Ha ezt nem fogjuk el, a kivétel
        /// a webhook, illetve a felhasználói kérés feldolgozásáig szalad fel, és a nyilvántartási
        /// sor besorolás nélkül maradna - vagyis nem lehetne tudni, érdemes-e újrapróbálni.
        /// </summary>
        [Fact]
        public async Task ATimeout_IsTransient()
        {
            var handler = new StubHttpMessageHandler(_ => throw new TaskCanceledException("időtúllépés"));
            var (sut, _) = Create(handler);

            var outcome = await sut.FetchRangeAsync(Request());

            Assert.False(outcome.Success);
            Assert.Equal(GitFetchErrorKind.Transient, outcome.ErrorKind);
        }

        /// <summary>
        /// Egy hiányos commit (sha vagy üzenet nélkül) nem viheti magával a lapot, és nem is
        /// jelentheti az intervallum végét: a lapozás a szolgáltató által adott lap HOSSZÁBÓL
        /// dönt, nem a sikeresen beolvasott elemek számából.
        /// </summary>
        [Fact]
        public async Task AnIncompleteCommit_IsSkippedWithoutEndingTheWalk()
        {
            var brokenPage = $$"""
            { "commits": [ {{string.Join(",", Enumerable.Range(1, 99).Select(Commit))}}, { "sha": "" } ] }
            """;

            var (sut, handler) = Create(StubHttpMessageHandler.Paging(page => page == 1 ? brokenPage : Page(0)));

            var outcome = await sut.FetchRangeAsync(Request());

            Assert.Equal(99, outcome.Commits.Count);
            Assert.Equal(2, handler.Requests.Count);
        }
    }
}
