using ProjectManager.API.Services.GitBackfillService;
using System.Net;

namespace ProjectManager.Tests.Services.GitBackfill
{
    /// <summary>
    /// A szolgáltatói hibaválaszok besorolása.
    ///
    /// Azért van jelentősége, mert a három osztályhoz három különböző válasz tartozik:
    /// az átmeneti hibánál érdemes újrapróbálni, a hitelesítési hibánál tokent kell cserélni
    /// (addig minden kísérlet ugyanide fut), a véglegesnél pedig nincs mit tenni.
    /// </summary>
    public class GitFetchErrorClassifierTests
    {
        private static GitFetchOutcome? Classify(
            HttpStatusCode status, params (string Name, string Value)[] headers) =>
            GitFetchErrorClassifier.Classify(StubHttpMessageHandler.Response("{}", status, headers));

        [Theory]
        [InlineData(HttpStatusCode.OK)]
        [InlineData(HttpStatusCode.Created)]
        public void SuccessfulResponse_IsNotAFailure(HttpStatusCode status)
        {
            Assert.Null(Classify(status));
        }

        [Fact]
        public void Unauthorized_IsAnAuthenticationFailure()
        {
            Assert.Equal(GitFetchErrorKind.Authentication, Classify(HttpStatusCode.Unauthorized)!.ErrorKind);
        }

        /// <summary>
        /// A 403 a GitHubnál kétértelmű: jogosultsági hiba VAGY elfogyott kvóta. A kettőre
        /// ellentétes a helyes válasz, ezért a maradék kvóta fejléce dönt - enélkül egy
        /// kifogyott kvóta „cseréld a tokent" üzenetet adna, ami téves nyomra vezet.
        /// </summary>
        [Fact]
        public void Forbidden_WithoutRateLimitHeader_IsAnAuthenticationFailure()
        {
            Assert.Equal(GitFetchErrorKind.Authentication, Classify(HttpStatusCode.Forbidden)!.ErrorKind);
        }

        [Fact]
        public void Forbidden_WithExhaustedRateLimit_IsTransient()
        {
            var outcome = Classify(HttpStatusCode.Forbidden, ("x-ratelimit-remaining", "0"))!;

            Assert.Equal(GitFetchErrorKind.Transient, outcome.ErrorKind);
            Assert.Contains("korlát", outcome.ErrorMessage);
        }

        [Fact]
        public void Forbidden_WithRemainingQuota_IsStillAnAuthenticationFailure()
        {
            Assert.Equal(
                GitFetchErrorKind.Authentication,
                Classify(HttpStatusCode.Forbidden, ("x-ratelimit-remaining", "42"))!.ErrorKind);
        }

        //A két szolgáltató más fejlécnevet használ ugyanerre
        [Fact]
        public void Forbidden_WithTheOtherProvidersRateLimitHeader_IsTransient()
        {
            Assert.Equal(
                GitFetchErrorKind.Transient,
                Classify(HttpStatusCode.Forbidden, ("ratelimit-remaining", "0"))!.ErrorKind);
        }

        /// <summary>
        /// A hibaszöveg mindkét lehetőséget megnevezi, mert a GitHub a privát tárházak létét
        /// nem fedi fel: jogosultság nélkül is 404 a válasz, nem 403.
        /// </summary>
        [Fact]
        public void NotFound_MentionsBothCauses()
        {
            var outcome = Classify(HttpStatusCode.NotFound)!;

            Assert.Equal(GitFetchErrorKind.NotFound, outcome.ErrorKind);
            Assert.Contains("token", outcome.ErrorMessage);
        }

        [Theory]
        [InlineData(HttpStatusCode.TooManyRequests)]
        [InlineData(HttpStatusCode.InternalServerError)]
        [InlineData(HttpStatusCode.BadGateway)]
        [InlineData(HttpStatusCode.ServiceUnavailable)]
        [InlineData(HttpStatusCode.GatewayTimeout)]
        public void ProviderSideProblems_AreTransient(HttpStatusCode status)
        {
            Assert.Equal(GitFetchErrorKind.Transient, Classify(status)!.ErrorKind);
        }

        [Theory]
        [InlineData(HttpStatusCode.BadRequest)]
        [InlineData(HttpStatusCode.UnsupportedMediaType)]
        [InlineData(HttpStatusCode.UnprocessableEntity)]
        public void EverythingElse_IsPermanent(HttpStatusCode status)
        {
            var outcome = Classify(status)!;

            Assert.Equal(GitFetchErrorKind.Permanent, outcome.ErrorKind);

            //A státuszkód bekerül a szövegbe: enélkül a felületen egy megfoghatatlan
            //„sikertelen" állapot látszana, és a napló nélkül nem lehetne továbbjutni
            Assert.Contains(((int)status).ToString(), outcome.ErrorMessage);
        }
    }
}
