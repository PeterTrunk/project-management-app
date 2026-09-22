using ProjectManager.API.Common.Constants;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace ProjectManager.API.Services.GitWebhookService.Payloads
{
    /// <summary>
    /// A GitHub webhook payloadjainak olvasása.
    /// Dokumentáció: https://docs.github.com/en/webhooks/webhook-events-and-payloads
    /// </summary>
    public sealed class GitHubPayloadParser : IGitPayloadParser
    {
        public string Provider => GitProviders.GitHub;

        public string EventHeaderName => "X-GitHub-Event";

        public WebhookEventKind ResolveEventKind(string headerValue) => headerValue switch
        {
            "ping" => WebhookEventKind.Ping,
            "push" => WebhookEventKind.Push,
            "pull_request" => WebhookEventKind.PullRequest,
            _ => WebhookEventKind.Unknown
        };

        public bool TryParsePush(JsonElement payload, [NotNullWhen(true)] out GitPushEvent? result)
        {
            result = null;

            if (!payload.TryReadArray("commits", out var commits)) return false;

            result = CommitArrayReader.Read(commits);
            return true;
        }

        public bool TryParsePullRequest(JsonElement payload, [NotNullWhen(true)] out GitPullRequestEvent? result)
        {
            result = null;

            //A GitHub sok akciót küld ugyanezen az eseménynéven. (labeled, assigned, synchronize, review_requested, stb)
            //Ezeket jelenleg nem kezeljük.
            var action = payload.ReadOptionalString("action") switch
            {
                "opened" => GitPullRequestAction.Opened,
                "reopened" => GitPullRequestAction.Reopened,
                "closed" => GitPullRequestAction.Closed,
                "edited" => GitPullRequestAction.Edited,
                _ => (GitPullRequestAction?)null
            };
            if (action is null) return false;

            if (!payload.TryReadObject("pull_request", out var pr)) return false;
            if (!pr.TryReadInt("number", out var number)) return false;

            var state = ResolveState(action.Value, pr);

            result = new GitPullRequestEvent(
                Number: number,
                Title: pr.ReadOptionalString("title") ?? string.Empty,
                Description: pr.ReadOptionalString("body"),
                Url: pr.ReadOptionalString("html_url"),
                AuthorName: pr.TryReadObject("user", out var user)
                    ? user.ReadOptionalString("login") ?? string.Empty
                    : string.Empty,
                Action: action.Value,
                State: state,
                MergedAt: state == GitPrStates.Merged ? pr.ReadTimestamp("merged_at") : null);

            return true;
        }

        private static string ResolveState(GitPullRequestAction action, JsonElement pr)
        {
            //A merged jelző a legerősebb: egy mergelt PR-t utólag szerkeszteni lehet, de visszanyitni nem
            if (pr.ReadBool("merged")) return GitPrStates.Merged;

            return action switch
            {
                GitPullRequestAction.Opened or GitPullRequestAction.Reopened => GitPrStates.Open,
                GitPullRequestAction.Closed => GitPrStates.Closed,

                //Szerkesztésnél az akcióból NEM következik állapot, ezért a PR saját mezője dönt.
                //Enélkül egy lezárt PR címének javítása visszaállítaná nyitottra a felületen.
                _ => pr.ReadOptionalString("state") == "closed"
                    ? GitPrStates.Closed
                    : GitPrStates.Open
            };
        }
    }
}
