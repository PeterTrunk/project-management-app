using ProjectManager.API.Services.GitWebhookService.Payloads;
using System.Text.Json;

namespace ProjectManager.Tests.Services.GitWebhook
{
    /// <summary>
    /// A szolgáltatói csonkolás észlelése.
    ///
    /// A git szolgáltatók nem küldik el minden push minden commitját: a GitLab 20, a GitHub
    /// 2048 commitnál vágja le a payload commits tömbjét. Enélkül az észlelés nélkül ez NÉMA
    /// adatvesztés - ha a task kulcsa épp egy levágott commit üzenetében van, a kapcsolat soha
    /// nem jön létre, és semmi nem jelzi.
    ///
    /// A két szolgáltató két külön esetet ad, és ezért van a döntés a parserben:
    /// a GitLab megmondja a valódi darabszámot (tehát a csonkolás TÉNY),
    /// a GitHub nem közöl darabszámot (tehát csak a tömb hosszából SEJTHETŐ).
    /// </summary>
    public class PushTruncationDetectionTests
    {
        private static readonly GitLabPayloadParser GitLab = new();
        private static readonly GitHubPayloadParser GitHub = new();

        private static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement;

        private static string Commit(int index) => $$"""
        {
          "id": "{{index:x40}}",
          "message": "commit {{index}}",
          "timestamp": "2026-09-16T14:27:31+02:00",
          "author": { "name": "Teszt Elek", "email": "teszt@example.com" }
        }
        """;

        private static string GitHubPush(int commitCount) => $$"""
        {
          "ref": "refs/heads/main",
          "before": "1111111111111111111111111111111111111111",
          "after": "2222222222222222222222222222222222222222",
          "commits": [ {{string.Join(",", Enumerable.Range(1, commitCount).Select(Commit))}} ]
        }
        """;

        private static string GitLabPush(string totalCommitsCount, int commitCount = 1) => $$"""
        {
          "object_kind": "push",
          "ref": "refs/heads/main",
          "before": "1111111111111111111111111111111111111111",
          "after": "2222222222222222222222222222222222222222",
          "commits": [ {{string.Join(",", Enumerable.Range(1, commitCount).Select(Commit))}} ],
          "total_commits_count": {{totalCommitsCount}}
        }
        """;

        //GitLab: a darabszám kiolvasható, tehát a csonkolás nem sejtés

        [Fact]
        public void GitLab_ReportedMoreThanReceived_IsTruncated()
        {
            Assert.True(GitLab.TryParsePush(Json(GitLabPush("25", commitCount: 20)), out var result));

            Assert.True(result.CommitsPossiblyTruncated);
            Assert.Equal(25, result.ProviderReportedCommitCount);
            Assert.Equal(20, result.ReceivedCommitCount);
        }

        [Fact]
        public void GitLab_ReportedEqualsReceived_IsNotTruncated()
        {
            Assert.True(GitLab.TryParsePush(Json(GitLabPush("3", commitCount: 3)), out var result));

            Assert.False(result.CommitsPossiblyTruncated);
            Assert.Equal(3, result.ProviderReportedCommitCount);
        }

        /// <summary>
        /// Darabszám nélkül nem állítjuk, hogy hiányzik valami: a mező hiánya nem bizonyíték.
        /// Egy téves jelzés arra bírná a felhasználót, hogy fölöslegesen kérjen le adatot a
        /// szolgáltatótól, kvótát égetve.
        /// </summary>
        [Fact]
        public void GitLab_MissingCommitCount_IsNotTruncated()
        {
            var payload = """
            {
              "object_kind": "push",
              "ref": "refs/heads/main",
              "commits": [ { "id": "aabbcc", "message": "PMA-1" } ]
            }
            """;

            Assert.True(GitLab.TryParsePush(Json(payload), out var result));

            Assert.False(result.CommitsPossiblyTruncated);
            Assert.Null(result.ProviderReportedCommitCount);
        }

        /// <summary>
        /// A két darabszám KÉT KÜLÖN dolgot jelent, és ez a teszt tartja szét őket:
        /// a SkippedCommitCount azt számolja, amit a MI parserünk nem tudott elolvasni
        /// (de megérkezett), a csonkolás pedig azt, amit a SZOLGÁLTATÓ nem küldött el.
        /// Egy értelmezhetetlen commit tehát nem csonkolás, és nem is pótolható lekéréssel.
        /// </summary>
        [Fact]
        public void GitLab_UnparseableCommitStillCountsAsReceived()
        {
            var payload = """
            {
              "object_kind": "push",
              "ref": "refs/heads/main",
              "commits": [
                { "id": "aabbcc", "message": "PMA-1" },
                { "id": "ddeeff" }
              ],
              "total_commits_count": 2
            }
            """;

            Assert.True(GitLab.TryParsePush(Json(payload), out var result));

            Assert.Equal(1, result.SkippedCommitCount);
            Assert.Equal(2, result.ReceivedCommitCount);
            Assert.False(result.CommitsPossiblyTruncated);
        }

        //GitHub: nincs darabszám, csak a felső határ

        [Fact]
        public void GitHub_ArrayAtTheCap_IsPossiblyTruncated()
        {
            Assert.True(GitHub.TryParsePush(Json(GitHubPush(2048)), out var result));

            Assert.True(result.CommitsPossiblyTruncated);

            //A GitHub nem közöl darabszámot: a hiány mértéke ismeretlen marad
            Assert.Null(result.ProviderReportedCommitCount);
            Assert.Equal(2048, result.ReceivedCommitCount);
        }

        [Fact]
        public void GitHub_ArrayBelowTheCap_IsNotTruncated()
        {
            Assert.True(GitHub.TryParsePush(Json(GitHubPush(2047)), out var result));

            Assert.False(result.CommitsPossiblyTruncated);
        }

        [Fact]
        public void GitHub_SmallPush_IsNotTruncated()
        {
            Assert.True(GitHub.TryParsePush(Json(GitHubPush(2)), out var result));

            Assert.False(result.CommitsPossiblyTruncated);
            Assert.Equal(2, result.ReceivedCommitCount);
        }

        //A pótló hívás paraméterei: a gomb KÉSŐBB indul, amikor a payload már nincs meg

        [Fact]
        public void GitHub_ReadsThePushIdentifiers()
        {
            Assert.True(GitHub.TryParsePush(Json(GitHubPush(1)), out var result));

            Assert.Equal("1111111111111111111111111111111111111111", result.BeforeSha);
            Assert.Equal("2222222222222222222222222222222222222222", result.AfterSha);
            Assert.Equal("refs/heads/main", result.Ref);
        }

        [Fact]
        public void GitLab_ReadsThePushIdentifiers()
        {
            Assert.True(GitLab.TryParsePush(Json(GitLabPush("1")), out var result));

            Assert.Equal("1111111111111111111111111111111111111111", result.BeforeSha);
            Assert.Equal("2222222222222222222222222222222222222222", result.AfterSha);
            Assert.Equal("refs/heads/main", result.Ref);
        }

        /// <summary>
        /// Hiányzó azonosítók mellett is el kell készülnie az eseménynek: a pótlás ilyenkor nem
        /// paraméterezhető, de a csonkolás tényét ettől még ki kell mondani.
        /// </summary>
        [Fact]
        public void MissingIdentifiers_DoNotBreakParsing()
        {
            var payload = """
            {
              "object_kind": "push",
              "commits": [ { "id": "aabbcc", "message": "PMA-1" } ],
              "total_commits_count": 9
            }
            """;

            Assert.True(GitLab.TryParsePush(Json(payload), out var result));

            Assert.True(result.CommitsPossiblyTruncated);
            Assert.Null(result.BeforeSha);
            Assert.Null(result.AfterSha);
            Assert.Null(result.Ref);
        }
    }
}
