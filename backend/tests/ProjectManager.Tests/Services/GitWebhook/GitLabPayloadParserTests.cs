using ProjectManager.API.Common.Constants;
using ProjectManager.API.Services.GitWebhookService.Payloads;
using System.Text.Json;

namespace ProjectManager.Tests.Services.GitWebhook
{
    /// <summary>
    /// A GitLab payloadok olvasása.
    ///
    /// Ezek a tesztek egy VALÓDI, éles hibát zárnak le: 
    /// A merge request eseményt korábban ugyanaz a GitHub-specifikus kód olvasta, 
    /// ami a gyökérben keresett action és pull_request mezőt, a GitLab payloadban egyik sincs, 
    /// tehát minden merge request kivétellel szállt el, és a GitLab hibásnak jelölte a webhookot.
    /// </summary>
    public class GitLabPayloadParserTests
    {
        private static readonly GitLabPayloadParser Sut = new();

        private static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement;

        //Esemény felismerés

        [Theory]
        [InlineData("Push Hook", WebhookEventKind.Push)]
        [InlineData("Merge Request Hook", WebhookEventKind.PullRequest)]
        [InlineData("Tag Push Hook", WebhookEventKind.Unknown)]
        [InlineData("Issue Hook", WebhookEventKind.Unknown)]
        [InlineData("Pipeline Hook", WebhookEventKind.Unknown)]
        [InlineData("", WebhookEventKind.Unknown)]
        public void ResolveEventKind_MapsTheHeader(string header, WebhookEventKind expected)
        {
            Assert.Equal(expected, Sut.ResolveEventKind(header));
        }

        //A GitLab a próbaeseményt valódi Push Hookként küldi, nincs külön ping
        [Fact]
        public void ResolveEventKind_NeverReportsPing()
        {
            string[] everyGitLabHook =
            [
                "Push Hook", "Tag Push Hook", "Merge Request Hook",
                "Issue Hook", "Note Hook", "Pipeline Hook", "Job Hook"
            ];

            Assert.DoesNotContain(
                everyGitLabHook.Select(Sut.ResolveEventKind),
                kind => kind == WebhookEventKind.Ping);
        }

        [Fact]
        public void Provider_IsGitLab()
        {
            Assert.Equal(GitProviders.GitLab, Sut.Provider);
            Assert.Equal("X-Gitlab-Event", Sut.EventHeaderName);
        }

        //Push
        private const string PushPayload = """
        {
          "object_kind": "push",
          "event_name": "push",
          "ref": "refs/heads/main",
          "project": { "path_with_namespace": "peter/pma" },
          "commits": [
            {
              "id": "aabbccddeeff00112233445566778899aabbccdd",
              "message": "PMA-1 hibajavítás",
              "timestamp": "2026-09-16T14:27:31+02:00",
              "url": "https://gitlab.com/peter/pma/-/commit/aabbccdd",
              "author": { "name": "Teszt Elek", "email": "teszt@example.com" }
            }
          ],
          "total_commits_count": 1
        }
        """;

        [Fact]
        public void TryParsePush_ValidPayload_MapsEveryField()
        {
            Assert.True(Sut.TryParsePush(Json(PushPayload), out var result));

            var commit = Assert.Single(result.Commits);
            Assert.Equal("aabbccddeeff00112233445566778899aabbccdd", commit.Sha);
            Assert.Equal("PMA-1 hibajavítás", commit.Message);
            Assert.Equal("https://gitlab.com/peter/pma/-/commit/aabbccdd", commit.Url);
            Assert.Equal("Teszt Elek", commit.AuthorName);
            Assert.Equal("teszt@example.com", commit.AuthorEmail);
            Assert.Equal(new DateTime(2026, 9, 16, 12, 27, 31, DateTimeKind.Utc), commit.CommittedAt);
        }

        //A címke-push külön object_kind, de a "Tag Push Hook" fejléc már kiszűri.
        //Az object_kind a második zár:
        //a fejléc a kérés küldőjétől jön, az object_kind viszont magából a payloadból, amit az aláírás hitelesít.
        [Fact]
        public void TryParsePush_TagPush_Fails()
        {
            var payload = """
            {
              "object_kind": "tag_push",
              "commits": [ { "id": "aabbcc", "message": "v1.0" } ]
            }
            """;

            Assert.False(Sut.TryParsePush(Json(payload), out _));
        }

        [Fact]
        public void TryParsePush_MissingObjectKind_Fails()
        {
            var payload = """
            { "commits": [ { "id": "aabbcc", "message": "PMA-1" } ] }
            """;

            Assert.False(Sut.TryParsePush(Json(payload), out _));
        }

        //Merge request
        private static string MergeRequestPayload(
            string action,
            string state = "opened",
            string mergedAt = "null",
            string updatedAt = "\"2026-09-16 12:05:00 UTC\"") => $$"""
        {
          "object_kind": "merge_request",
          "event_type": "merge_request",
          "user": { "id": 1, "name": "Teszt Elek", "username": "teszt-elek" },
          "project": { "id": 7, "path_with_namespace": "peter/pma" },
          "object_attributes": {
            "id": 99,
            "iid": 42,
            "title": "PMA-1 hibajavítás",
            "description": "A leírásban is van egy PMA-2 kulcs.",
            "url": "https://gitlab.com/peter/pma/-/merge_requests/42",
            "source_branch": "fix/pma-1",
            "target_branch": "main",
            "state": "{{state}}",
            "action": "{{action}}",
            "created_at": "2026-09-16 12:00:00 UTC",
            "updated_at": {{updatedAt}},
            "merged_at": {{mergedAt}}
          }
        }
        """;

        /// <summary>
        /// A regressziós teszt. 
        /// Korábban ez a payload kivételt dobott, mert a feldolgozó a gyökérben keresett "action"-t és "pull_request"-et.
        /// </summary>
        [Fact]
        public void TryParsePullRequest_Opened_MapsEveryField()
        {
            Assert.True(Sut.TryParsePullRequest(Json(MergeRequestPayload("open")), out var result));

            //Az iid a felhasználó által látott sorszám, nem a globális id (99)
            Assert.Equal(42, result.Number);
            Assert.Equal("PMA-1 hibajavítás", result.Title);
            Assert.Equal("A leírásban is van egy PMA-2 kulcs.", result.Description);
            Assert.Equal("https://gitlab.com/peter/pma/-/merge_requests/42", result.Url);
            //A szerzőnév a gyökér user objektumából jön, nem az object_attributes-ból
            Assert.Equal("Teszt Elek", result.AuthorName);
            Assert.Equal(GitPullRequestAction.Opened, result.Action);
            Assert.Equal(GitPrStates.Open, result.State);
            Assert.Null(result.MergedAt);
        }

        [Theory]
        [InlineData("open", "opened", GitPullRequestAction.Opened, GitPrStates.Open)]
        [InlineData("reopen", "opened", GitPullRequestAction.Reopened, GitPrStates.Open)]
        [InlineData("close", "closed", GitPullRequestAction.Closed, GitPrStates.Closed)]
        [InlineData("merge", "merged", GitPullRequestAction.Closed, GitPrStates.Merged)]
        [InlineData("update", "opened", GitPullRequestAction.Edited, GitPrStates.Open)]
        public void TryParsePullRequest_TranslatesGitLabVocabulary(
            string gitLabAction, string gitLabState,
            GitPullRequestAction expectedAction, string expectedState)
        {
            var payload = MergeRequestPayload(gitLabAction, gitLabState);

            Assert.True(Sut.TryParsePullRequest(Json(payload), out var result));

            Assert.Equal(expectedAction, result.Action);
            Assert.Equal(expectedState, result.State);
        }

        //Szerkesztés egy már mergelt merge requesten:
        //Az MR saját állapota dönt, nem az akció Különben a felületen visszaállna nyitottra
        [Fact]
        public void TryParsePullRequest_UpdateOnMergedRequest_StaysMerged()
        {
            var payload = MergeRequestPayload("update", state: "merged");

            Assert.True(Sut.TryParsePullRequest(Json(payload), out var result));

            Assert.Equal(GitPrStates.Merged, result.State);
        }

        //A GitLab merge közben zárolja a merge requestet: ez átmeneti állapot, nem végállapot
        [Fact]
        public void TryParsePullRequest_LockedState_IsTreatedAsOpen()
        {
            var payload = MergeRequestPayload("update", state: "locked");

            Assert.True(Sut.TryParsePullRequest(Json(payload), out var result));

            Assert.Equal(GitPrStates.Open, result.State);
        }

        [Fact]
        public void TryParsePullRequest_Merged_ReadsMergedAt()
        {
            var payload = MergeRequestPayload("merge", state: "merged",
                mergedAt: "\"2026-09-16 12:30:00 UTC\"");

            Assert.True(Sut.TryParsePullRequest(Json(payload), out var result));

            Assert.Equal(new DateTime(2026, 9, 16, 12, 30, 0, DateTimeKind.Utc), result.MergedAt);
        }

        //A GitLab nem minden verzióban küld merged_at-ot: ilyenkor az updated_at a merge pillanata.
        //A dátumformátum itt NEM ISO 8601 ("2026-09-16 12:05:00 UTC"),
        //amit az általános elemző elutasít, ezt a fallback formátum fogja meg.
        [Fact]
        public void TryParsePullRequest_MergedWithoutMergedAt_FallsBackToUpdatedAt()
        {
            var payload = MergeRequestPayload("merge", state: "merged", mergedAt: "null");

            Assert.True(Sut.TryParsePullRequest(Json(payload), out var result));

            Assert.Equal(new DateTime(2026, 9, 16, 12, 5, 0, DateTimeKind.Utc), result.MergedAt);
        }

        [Fact]
        public void TryParsePullRequest_NotMerged_HasNoMergeTimestamp()
        {
            var payload = MergeRequestPayload("close", state: "closed");

            Assert.True(Sut.TryParsePullRequest(Json(payload), out var result));

            Assert.Null(result.MergedAt);
        }

        //A jóváhagyási események nem változtatják sem a címet, sem az állapotot
        [Theory]
        [InlineData("approved")]
        [InlineData("unapproved")]
        [InlineData("approval")]
        [InlineData("unapproval")]
        public void TryParsePullRequest_ApprovalActions_Fail(string action)
        {
            Assert.False(Sut.TryParsePullRequest(Json(MergeRequestPayload(action)), out _));
        }

        [Fact]
        public void TryParsePullRequest_WrongObjectKind_Fails()
        {
            var payload = """
            {
              "object_kind": "note",
              "object_attributes": { "iid": 42, "action": "open" }
            }
            """;

            Assert.False(Sut.TryParsePullRequest(Json(payload), out _));
        }

        [Fact]
        public void TryParsePullRequest_MissingObjectAttributes_Fails()
        {
            Assert.False(Sut.TryParsePullRequest(Json("""{ "object_kind": "merge_request" }"""), out _));
        }

        [Fact]
        public void TryParsePullRequest_MissingIid_Fails()
        {
            var payload = """
            {
              "object_kind": "merge_request",
              "object_attributes": { "id": 99, "action": "open", "title": "iid nélkül" }
            }
            """;

            Assert.False(Sut.TryParsePullRequest(Json(payload), out _));
        }
    }
}
