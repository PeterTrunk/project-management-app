using ProjectManager.API.Common.Constants;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace ProjectManager.API.Services.GitWebhookService.Payloads
{
    /// <summary>
    /// A GitLab webhook payloadjainak olvasása.
    /// Dokumentáció: https://docs.gitlab.com/user/project/integrations/webhook_events/
    ///
    /// Ez az osztály az oka az egész normalizáló rétegnek. 
    /// A merge request payload a GitHub-tól jelentősen eltér, más a gyökérelem neve, 
    /// más a sorszám mezője, és az akciónevek is mások, korábban viszont ugyanaz a GitHub-specifikus kód olvasta.
    /// 
    /// Emiatt ez előző állapot egyáltalán nem tudta támogatni a GitLab-ot:
    /// Minden GitLab merge request kivétellel szállt el, még mielőtt bármit csinált volna.
    /// </summary>
    public sealed class GitLabPayloadParser : IGitPayloadParser
    {
        public string Provider => GitProviders.GitLab;

        public string EventHeaderName => "X-Gitlab-Event";

        public WebhookEventKind ResolveEventKind(string headerValue) => headerValue switch
        {
            "Push Hook" => WebhookEventKind.Push,
            "Merge Request Hook" => WebhookEventKind.PullRequest,

            //A GitLabnak nincs ping eseménye: a "Test" (setupkor) valódi Push Hookot küld.
            _ => WebhookEventKind.Unknown
        };

        public bool TryParsePush(JsonElement payload, [NotNullWhen(true)] out GitPushEvent? result)
        {
            result = null;

            //Az object_kind a megbízható megkülönböztető:
            //A fejléc átírható, a payloadban viszont a GitLab maga mondja meg, mit küldött. A címke-push külön "tag_push",
            //és nem akarunk címkéket commitként feldolgozni.
            if (payload.ReadOptionalString("object_kind") != "push") return false;
            if (!payload.TryReadArray("commits", out var commits)) return false;

            result = CommitArrayReader.Read(commits);
            return true;
        }

        public bool TryParsePullRequest(JsonElement payload, [NotNullWhen(true)] out GitPullRequestEvent? result)
        {
            result = null;

            if (payload.ReadOptionalString("object_kind") != "merge_request") return false;

            //A GitHub "pull_request" gyökérelemének megfelelője
            if (!payload.TryReadObject("object_attributes", out var mr)) return false;

            //A GitLab az akciót a gyökér helyett ITT küldi, és más szavakkal.
            //Amit kihagyunk: approved, unapproved, approval, unapproval - ezek nem
            //változtatják sem a címet, sem az állapotot.
            var action = mr.ReadOptionalString("action") switch
            {
                "open" => GitPullRequestAction.Opened,
                "reopen" => GitPullRequestAction.Reopened,
                "close" => GitPullRequestAction.Closed,
                "merge" => GitPullRequestAction.Closed,
                "update" => GitPullRequestAction.Edited,
                _ => (GitPullRequestAction?)null
            };
            if (action is null) return false;

            //A felhasználó által látott sorszám az iid;
            //az id a GitLab globális azonosítója, ami a linkekben sosem szerepel
            if (!mr.TryReadInt("iid", out var number)) return false;

            var state = ResolveState(action.Value, mr);

            result = new GitPullRequestEvent(
                Number: number,
                Title: mr.ReadOptionalString("title") ?? string.Empty,
                Description: mr.ReadOptionalString("description"),
                Url: mr.ReadOptionalString("url"),

                //A szerzőnév a gyökérben van.
                //Megnyitásnál ez maga a szerző; lezárásnál lehet más is. Az object_attributes
                //csak author_id-t hordoz, nevet nem, tehát ennél pontosabbat a payload nem ad.
                AuthorName: payload.TryReadObject("user", out var user)
                    ? user.ReadOptionalString("name") ?? string.Empty
                    : string.Empty,
                Action: action.Value,
                State: state,
                MergedAt: state == GitPrStates.Merged
                    //A GitLab nem minden verzióban küld merged_at-ot,
                    //az updated_at viszont merge esetén pont a merge pillanata
                    ? mr.ReadTimestamp("merged_at") ?? mr.ReadTimestamp("updated_at")
                    : null);

            return true;
        }

        private static string ResolveState(GitPullRequestAction action, JsonElement mr)
        {
            //A GitLab az MR saját állapotát is küldi, és az pontosabb az akciónál:
            //az "update" akcióból önmagából nem derül ki, nyitott-e még a merge request
            return mr.ReadOptionalString("state") switch
            {
                "merged" => GitPrStates.Merged,
                "closed" => GitPrStates.Closed,

                //A "locked" átmeneti zárolás merge közben, nem végállapot
                "opened" or "locked" => GitPrStates.Open,

                _ => action == GitPullRequestAction.Closed
                    ? GitPrStates.Closed
                    : GitPrStates.Open
            };
        }
    }
}
