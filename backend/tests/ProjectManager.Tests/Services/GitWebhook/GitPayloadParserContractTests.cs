using ProjectManager.API.Common.Constants;
using ProjectManager.API.Services.GitWebhookService.Payloads;
using System.Reflection;
using System.Text.Json;

namespace ProjectManager.Tests.Services.GitWebhook
{
    /// <summary>
    /// Az egész normalizáló réteg értelme egy mondatban:
    /// a szolgáltatótól FÜGGETLENÜL ugyanaz jöjjön ki, és semmilyen payloadtól ne szálljon el.
    /// 
    /// Ezek a tesztek nem egy-egy parsert mérnek, hanem azt,
    /// ami mindegyikre igaz kell legyen, beleértve azt is amit valaki holnap vesz fel.
    /// </summary>
    public class GitPayloadParserContractTests
    {
        private static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement;

        /// <summary>Az API összeállításában található összes parser, ahogy a DI is megtalálja.</summary>
        private static readonly IGitPayloadParser[] AllParsers =
            typeof(IGitPayloadParser).Assembly
                .GetTypes()
                .Where(t => typeof(IGitPayloadParser).IsAssignableFrom(t) && t is { IsInterface: false, IsAbstract: false })
                .Select(t => (IGitPayloadParser)Activator.CreateInstance(t)!)
                .ToArray();

        public static TheoryData<string> ParserProviders()
        {
            var data = new TheoryData<string>();
            foreach (var parser in AllParsers) data.Add(parser.Provider);
            return data;
        }

        private static IGitPayloadParser ParserFor(string provider) =>
            AllParsers.Single(p => p.Provider == provider);

        /// <summary>
        /// Ha valaki felvesz egy providert a konstansok közé, de parsert nem ír hozzá, a
        /// webhookja csendben minden eseményt eldobna. Ez a teszt ezért bukik meg helyette.
        /// </summary>
        [Fact]
        public void EveryKnownProvider_HasExactlyOneParser()
        {
            foreach (var provider in GitProviders.All)
            {
                Assert.Single(AllParsers, p => p.Provider == provider);
            }

            Assert.Equal(GitProviders.All.Length, AllParsers.Length);
        }

        [Theory]
        [MemberData(nameof(ParserProviders))]
        public void EveryParser_NamesTheHeaderItReads(string provider)
        {
            var parser = ParserFor(provider);

            Assert.False(string.IsNullOrWhiteSpace(parser.EventHeaderName));
            Assert.StartsWith("X-", parser.EventHeaderName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Egy váratlan alakú, de érvényes JSON nem dobhat kivételt: 
        /// abból a végpont 500-at adna, a szolgáltató pedig ismétlődő hiba után kikapcsolja a webhookot.
        /// A helyes válasz a csendes elutasítás.
        /// </summary>
        [Theory]
        [MemberData(nameof(ParserProviders))]
        public void EveryParser_RejectsHostilePayloadsWithoutThrowing(string provider)
        {
            var parser = ParserFor(provider);

            string[] hostile =
            [
                "{}",
                "[]",
                "null",
                "42",
                "\"csak egy string\"",
                """{ "commits": null }""",
                """{ "commits": {} }""",
                """{ "commits": [ null, 42, "szöveg", [] ] }""",
                """{ "object_kind": null }""",
                """{ "action": 42, "pull_request": 42 }""",
                """{ "object_kind": "merge_request", "object_attributes": [] }""",
                """{ "object_kind": "merge_request", "object_attributes": { "iid": "nem szám", "action": "open" } }""",
                """{ "action": "opened", "pull_request": { "number": "nem szám" } }""",
                """{ "action": "opened", "pull_request": { "number": 1, "user": "nem objektum" } }""",
                """{ "commits": [ { "id": "aabbcc", "message": "x", "timestamp": "nem dátum", "author": 42 } ] }"""
            ];

            foreach (var raw in hostile)
            {
                var payload = Json(raw);

                //Ami számít: nincs kivétel. Hogy igazat vagy hamisat ad, payloadonként eltér -
                //az üres commit tömb például érvényes push.
                var pushEx = Record.Exception(() => parser.TryParsePush(payload, out _));
                Assert.Null(pushEx);

                var prEx = Record.Exception(() => parser.TryParsePullRequest(payload, out _));
                Assert.Null(prEx);
            }
        }

        [Theory]
        [MemberData(nameof(ParserProviders))]
        public void EveryParser_TreatsAnUnknownHeaderAsUnhandled(string provider)
        {
            var parser = ParserFor(provider);

            Assert.Equal(WebhookEventKind.Unknown, parser.ResolveEventKind(""));
            Assert.Equal(WebhookEventKind.Unknown, parser.ResolveEventKind("ilyen esemény nincs"));
        }

        /// <summary>
        /// A normalizálás próbája: a két szolgáltató SAJÁT alakú push payloadjából bitre ugyanaz a rekord jön ki.
        /// Ha ez igaz, a feldolgozó szolgáltatásnak tényleg nem kell tudnia, honnan jött az esemény.
        /// </summary>
        [Fact]
        public void BothProviders_ProduceTheSameNormalisedPush()
        {
            const string gitHubPush = """
            {
              "ref": "refs/heads/main",
              "repository": { "full_name": "peter/pma" },
              "commits": [
                {
                  "id": "aabbccddeeff00112233445566778899aabbccdd",
                  "message": "PMA-1 hibajavítás",
                  "timestamp": "2026-09-16T14:27:31+02:00",
                  "url": "https://example.com/commit/aabbccdd",
                  "author": { "name": "Teszt Elek", "email": "teszt@example.com", "username": "teszt" }
                }
              ]
            }
            """;

            const string gitLabPush = """
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
                  "url": "https://example.com/commit/aabbccdd",
                  "author": { "name": "Teszt Elek", "email": "teszt@example.com" }
                }
              ],
              "total_commits_count": 1
            }
            """;

            Assert.True(ParserFor(GitProviders.GitHub).TryParsePush(Json(gitHubPush), out var fromGitHub));
            Assert.True(ParserFor(GitProviders.GitLab).TryParsePush(Json(gitLabPush), out var fromGitLab));

            //A GitPushEvent egészét nem lehet egyben összevetni:
            //A rekord-egyenlőség a Commits listára a List<T> alapértelmezett, REFERENCIA szerinti egyenlőségét használná.
            //A listák elemenkénti összevetése viszont a GitCommitInfo rekord-egyenlőségére fut, tehát minden commit-mezőre szól.
            Assert.Equal(fromGitHub.SkippedCommitCount, fromGitLab.SkippedCommitCount);
            Assert.Equal(fromGitHub.Commits, fromGitLab.Commits);
        }

        /// <summary>
        /// Ugyanez a merge request oldalon. Itt a két payload semmiben nem hasonlít -
        /// pontosan ez volt az eredeti hiba oka.
        /// </summary>
        [Fact]
        public void BothProviders_ProduceTheSameNormalisedPullRequest()
        {
            const string gitHubPr = """
            {
              "action": "opened",
              "pull_request": {
                "number": 42,
                "title": "PMA-1 hibajavítás",
                "body": "Leírás.",
                "html_url": "https://example.com/pr/42",
                "state": "open",
                "merged": false,
                "user": { "login": "Teszt Elek" }
              }
            }
            """;

            const string gitLabMr = """
            {
              "object_kind": "merge_request",
              "user": { "name": "Teszt Elek" },
              "object_attributes": {
                "id": 99,
                "iid": 42,
                "title": "PMA-1 hibajavítás",
                "description": "Leírás.",
                "url": "https://example.com/pr/42",
                "state": "opened",
                "action": "open"
              }
            }
            """;

            Assert.True(ParserFor(GitProviders.GitHub).TryParsePullRequest(Json(gitHubPr), out var fromGitHub));
            Assert.True(ParserFor(GitProviders.GitLab).TryParsePullRequest(Json(gitLabMr), out var fromGitLab));

            //Itt a rekord minden mezője érték típusú vagy string, tehát egyben összevethető
            Assert.Equal(fromGitHub, fromGitLab);
        }
    }
}
