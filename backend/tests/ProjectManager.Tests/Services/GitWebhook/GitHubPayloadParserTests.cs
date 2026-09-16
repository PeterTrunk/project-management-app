using ProjectManager.API.Common.Constants;
using ProjectManager.API.Services.GitWebhookService.Payloads;
using System.Text.Json;

namespace ProjectManager.Tests.Services.GitWebhook
{
    /// <summary>
    /// A GitHub payloadok olvasása. 
    /// A parser tiszta függvény: se adatbázis, se hálózat, tehát a mintapayload önmagában elegendő bemenet.
    /// 
    /// A payloadok a GitHub dokumentációjának alakját követik, de csak azokat a mezőket tartalmazzák, amiket olvasunk
    /// - a teljes payload több száz soros, és a többi mező jelenléte semmit nem bizonyítana.
    /// </summary>
    public class GitHubPayloadParserTests
    {
        private static readonly GitHubPayloadParser Sut = new();

        private static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement;

        //Esemény felismerés
        [Theory]
        [InlineData("ping", WebhookEventKind.Ping)]
        [InlineData("push", WebhookEventKind.Push)]
        [InlineData("pull_request", WebhookEventKind.PullRequest)]
        [InlineData("issues", WebhookEventKind.Unknown)]
        [InlineData("workflow_run", WebhookEventKind.Unknown)]
        [InlineData("", WebhookEventKind.Unknown)]
        public void ResolveEventKind_MapsTheHeader(string header, WebhookEventKind expected)
        {
            Assert.Equal(expected, Sut.ResolveEventKind(header));
        }

        [Fact]
        public void Provider_IsGitHub()
        {
            Assert.Equal(GitProviders.GitHub, Sut.Provider);
            Assert.Equal("X-GitHub-Event", Sut.EventHeaderName);
        }

        //Push
        private const string PushPayload = """
        {
          "ref": "refs/heads/main",
          "repository": { "full_name": "peter/pma" },
          "commits": [
            {
              "id": "aabbccddeeff00112233445566778899aabbccdd",
              "message": "PMA-1 hibajavítás",
              "timestamp": "2026-09-16T14:27:31+02:00",
              "url": "https://github.com/peter/pma/commit/aabbccdd",
              "author": { "name": "Teszt Elek", "email": "teszt@example.com", "username": "teszt" }
            },
            {
              "id": "1122334455667788990011223344556677889900",
              "message": "PMA-2 második",
              "timestamp": "2026-09-16T15:00:00Z",
              "url": "https://github.com/peter/pma/commit/11223344",
              "author": { "name": "Másik Elek", "email": "masik@example.com" }
            }
          ]
        }
        """;

        [Fact]
        public void TryParsePush_ValidPayload_MapsEveryField()
        {
            Assert.True(Sut.TryParsePush(Json(PushPayload), out var result));

            Assert.Equal(2, result.Commits.Count);
            Assert.Equal(0, result.SkippedCommitCount);

            var first = result.Commits[0];
            Assert.Equal("aabbccddeeff00112233445566778899aabbccdd", first.Sha);
            Assert.Equal("PMA-1 hibajavítás", first.Message);
            Assert.Equal("https://github.com/peter/pma/commit/aabbccdd", first.Url);
            Assert.Equal("Teszt Elek", first.AuthorName);
            Assert.Equal("teszt@example.com", first.AuthorEmail);
        }

        //Az eltolásos időbélyeg a régi DateTime.Parse hívásnál a futtató gép "kultúrájától" függött;
        //Itt az eredménynek kultúrától függetlenül UTC-nek kell lennie
        [Fact]
        public void TryParsePush_OffsetTimestamp_ConvertsToUtc()
        {
            Assert.True(Sut.TryParsePush(Json(PushPayload), out var result));

            var committedAt = result.Commits[0].CommittedAt;
            Assert.Equal(DateTimeKind.Utc, committedAt.Kind);
            Assert.Equal(new DateTime(2026, 9, 16, 12, 27, 31, DateTimeKind.Utc), committedAt);
        }

        [Fact]
        public void TryParsePush_EmptyCommitArray_SucceedsWithNothingToDo()
        {
            //Ág törlésekor a szolgáltató üres tömböt küld: ez érvényes push, nem hiba
            Assert.True(Sut.TryParsePush(Json("""{ "commits": [] }"""), out var result));

            Assert.Empty(result.Commits);
        }

        [Fact]
        public void TryParsePush_MissingCommitArray_Fails()
        {
            Assert.False(Sut.TryParsePush(Json("""{ "ref": "refs/heads/main" }"""), out _));
        }

        [Fact]
        public void TryParsePush_CommitsIsNotAnArray_Fails()
        {
            Assert.False(Sut.TryParsePush(Json("""{ "commits": "nem tömb" }"""), out _));
        }

        //Egy használhatatlan commit ne vigye magával a többit,
        //de a kihagyás ne is legyen néma, különben a push feldolgozottnak látszik hiányzó adattal
        [Fact]
        public void TryParsePush_CommitWithoutSha_IsSkippedAndCounted()
        {
            var payload = """
            {
              "commits": [
                { "message": "sha nélkül" },
                {
                  "id": "aabbccddeeff00112233445566778899aabbccdd",
                  "message": "PMA-1 rendben",
                  "author": { "name": "Teszt Elek", "email": "teszt@example.com" }
                }
              ]
            }
            """;

            Assert.True(Sut.TryParsePush(Json(payload), out var result));

            Assert.Single(result.Commits);
            Assert.Equal(1, result.SkippedCommitCount);
            Assert.Equal("PMA-1 rendben", result.Commits[0].Message);
        }

        [Fact]
        public void TryParsePush_MissingOptionalFields_FallsBackWithoutThrowing()
        {
            var payload = """
            {
              "commits": [
                { "id": "aabbccddeeff0011", "message": "minimális" }
              ]
            }
            """;

            Assert.True(Sut.TryParsePush(Json(payload), out var result));

            var commit = Assert.Single(result.Commits);
            Assert.Null(commit.Url);
            Assert.Equal(string.Empty, commit.AuthorName);
            Assert.Equal(string.Empty, commit.AuthorEmail);
            //Hiányzó időbélyeg esetén a feldolgozás ideje
            Assert.InRange(commit.CommittedAt, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
        }

        //Pull request

        private static string PrPayload(
            string action,
            bool merged = false,
            string state = "open",
            string mergedAt = "null") => $$"""
        {
          "action": "{{action}}",
          "repository": { "full_name": "peter/pma" },
          "pull_request": {
            "number": 42,
            "title": "PMA-1 hibajavítás",
            "body": "A leírásban is van egy PMA-2 kulcs.",
            "html_url": "https://github.com/peter/pma/pull/42",
            "state": "{{state}}",
            "merged": {{(merged ? "true" : "false")}},
            "merged_at": {{mergedAt}},
            "user": { "login": "teszt-elek" }
          }
        }
        """;

        [Fact]
        public void TryParsePullRequest_Opened_MapsEveryField()
        {
            Assert.True(Sut.TryParsePullRequest(Json(PrPayload("opened")), out var result));

            Assert.Equal(42, result.Number);
            Assert.Equal("PMA-1 hibajavítás", result.Title);
            Assert.Equal("A leírásban is van egy PMA-2 kulcs.", result.Description);
            Assert.Equal("https://github.com/peter/pma/pull/42", result.Url);
            Assert.Equal("teszt-elek", result.AuthorName);
            Assert.Equal(GitPullRequestAction.Opened, result.Action);
            Assert.Equal(GitPrStates.Open, result.State);
            Assert.Null(result.MergedAt);
        }

        [Fact]
        public void TryParsePullRequest_ClosedAndMerged_IsMergedWithTimestamp()
        {
            var payload = PrPayload("closed", merged: true, state: "closed",
                mergedAt: "\"2026-09-16T14:27:31+02:00\"");

            Assert.True(Sut.TryParsePullRequest(Json(payload), out var result));

            Assert.Equal(GitPrStates.Merged, result.State);
            Assert.Equal(new DateTime(2026, 9, 16, 12, 27, 31, DateTimeKind.Utc), result.MergedAt);
        }

        [Fact]
        public void TryParsePullRequest_ClosedWithoutMerge_IsClosed()
        {
            var payload = PrPayload("closed", merged: false, state: "closed");

            Assert.True(Sut.TryParsePullRequest(Json(payload), out var result));

            Assert.Equal(GitPrStates.Closed, result.State);
            Assert.Null(result.MergedAt);
        }

        [Fact]
        public void TryParsePullRequest_Reopened_IsOpen()
        {
            Assert.True(Sut.TryParsePullRequest(Json(PrPayload("reopened")), out var result));

            Assert.Equal(GitPullRequestAction.Reopened, result.Action);
            Assert.Equal(GitPrStates.Open, result.State);
        }

        //Ez a lényegi eset: a szerkesztés akciójából NEM következik állapot.
        //Korábban minden nem-closed akció "open"-t adott, tehát egy lezárt PR címének javítása visszaállította nyitottra a felületen.
        [Theory]
        [InlineData("open", false, GitPrStates.Open)]
        [InlineData("closed", false, GitPrStates.Closed)]
        [InlineData("closed", true, GitPrStates.Merged)]
        public void TryParsePullRequest_Edited_KeepsThePullRequestsOwnState(
            string state, bool merged, string expected)
        {
            var payload = PrPayload("edited", merged: merged, state: state);

            Assert.True(Sut.TryParsePullRequest(Json(payload), out var result));

            Assert.Equal(GitPullRequestAction.Edited, result.Action);
            Assert.Equal(expected, result.State);
        }

        //A GitHub ugyanezen az eseménynéven küldi a címkézést, a hozzárendelést és a review kéréseket is, de ezekre nem reagálunk
        [Theory]
        [InlineData("labeled")]
        [InlineData("assigned")]
        [InlineData("synchronize")]
        [InlineData("review_requested")]
        public void TryParsePullRequest_UnhandledAction_Fails(string action)
        {
            Assert.False(Sut.TryParsePullRequest(Json(PrPayload(action)), out _));
        }

        [Fact]
        public void TryParsePullRequest_MissingPullRequestObject_Fails()
        {
            Assert.False(Sut.TryParsePullRequest(Json("""{ "action": "opened" }"""), out _));
        }

        [Fact]
        public void TryParsePullRequest_MissingNumber_Fails()
        {
            var payload = """
            { "action": "opened", "pull_request": { "title": "szám nélkül" } }
            """;

            Assert.False(Sut.TryParsePullRequest(Json(payload), out _));
        }

        //Egy GitLab merge request payload semmiképp ne csússzon át a GitHub parseren:
        //a csendes félreértelmezés rosszabb lenne, mint az elutasítás
        [Fact]
        public void TryParsePullRequest_GitLabShapedPayload_Fails()
        {
            var payload = """
            {
              "object_kind": "merge_request",
              "object_attributes": { "iid": 42, "action": "open", "title": "PMA-1" }
            }
            """;

            Assert.False(Sut.TryParsePullRequest(Json(payload), out _));
        }
    }
}
