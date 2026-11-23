using ProjectManager.API.Common.Constants;
using ProjectManager.API.Services.GitWebhookService.Payloads;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ProjectManager.API.Services.GitBackfillService
{
    /// <summary>
    /// Commitok lekérése a GitHub compare végpontjáról.
    /// Dokumentáció: https://docs.github.com/en/rest/commits/commits#compare-two-commits
    ///
    /// A végpont lapozás nélkül legfeljebb 250 commitot ad; a per_page és a page paraméterrel
    /// viszont végigjárható az intervallum, laponként legfeljebb 100 commitot kapva.
    /// (Ellenőrizve a dokumentációban: 2026. 11. 18.)
    /// </summary>
    public sealed class GitHubCommitFetcher : IGitCommitFetcher
    {
        private const string CloudApiRoot = "https://api.github.com";

        //A GitHub Enterprise Server API-ja a példány alatt, ezen az útvonalon érhető el
        private const string EnterpriseVersionPath = "/api/v3";

        //A lapméret felső határa a végponton
        private const int PageSize = 100;

        /// <summary>
        /// Felső határ a lapozásra. Enélkül egy nagy intervallum (például egy hosszú ág
        /// visszaolvasztása) korlátlan számú kimenő hívást indítana, felhasználói kérésben.
        /// A határba futást nem hallgatjuk el, hanem az eredményben jelezzük.
        /// </summary>
        private const int MaxPages = 10;

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<GitHubCommitFetcher> _logger;

        public GitHubCommitFetcher(IHttpClientFactory httpClientFactory, ILogger<GitHubCommitFetcher> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public string Provider => GitProviders.GitHub;

        public async Task<GitFetchOutcome> FetchRangeAsync(
            GitFetchRequest request, CancellationToken cancellationToken = default)
        {
            var root = GitApiRoot.Resolve(request.InstanceUrl, CloudApiRoot, EnterpriseVersionPath);
            var client = _httpClientFactory.CreateClient(GitApiHttpClient.Name);

            var commits = new List<GitCommitInfo>();

            for (var page = 1; page <= MaxPages; page++)
            {
                var url = $"{root}/repos/{request.RepoFullName}/compare/" +
                          $"{request.BeforeSha}...{request.AfterSha}?per_page={PageSize}&page={page}";

                var (outcome, document) = await SendAsync(
                    client, url, request.AccessToken,
                    isSelfHosted: !string.IsNullOrWhiteSpace(request.InstanceUrl),
                    cancellationToken);
                if (outcome != null) return outcome;

                using (document)
                {
                    if (!document!.RootElement.TryReadArray("commits", out var array))
                        return GitFetchOutcome.Fail(GitFetchErrorKind.Permanent,
                            "A szolgáltató válasza nem tartalmazott commitlistát.");

                    var countOnPage = ReadCommits(array, commits);

                    //A rövidebb lap az utolsó lap: itt ér véget az intervallum
                    if (countOnPage < PageSize)
                        return GitFetchOutcome.Ok(commits);
                }
            }

            //Minden lap tele volt, tehát feltehetően van még - ezt ki kell mondani
            _logger.LogWarning(
                "A GitHub commit lekérés a lapozási felső határba futott | Commitok: {Count} | Repo: {Repo}",
                commits.Count, request.RepoFullName);

            return GitFetchOutcome.Ok(commits, incomplete: true);
        }

        /// <summary>
        /// Egy lap lekérése. A visszatérés vagy egy hibát leíró eredmény, vagy a válasz
        /// dokumentuma - a kettő közül pontosan az egyik nem null.
        /// </summary>
        private async Task<(GitFetchOutcome? Outcome, JsonDocument? Document)> SendAsync(
            HttpClient client, string url, string? accessToken, bool isSelfHosted,
            CancellationToken cancellationToken)
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, url);

            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            //A verziót CSAK a felhőnél mondjuk ki. Ott azért érdemes, mert enélkül a
            //szolgáltató alapértelmezése dönt, és az a mi kódunk nélkül is változhat.
            //(A dokumentációban hatályos verzió, ellenőrizve: 2026. 11. 18.)
            //
            //Saját telepítésű GitHub Enterprise Servernél viszont NEM: az a példány a maga
            //kiadásához tartozó verziókat ismeri, és egy általa nem ismert verziószámtól
            //minden kérésünk elutasításra kerülne. Ilyenkor a példány válasszon.
            if (!isSelfHosted)
                message.Headers.Add("X-GitHub-Api-Version", "2026-03-10");

            //Token nélkül is van értelme: publikus tárházból olvasható a commitlista,
            //csak jóval szűkebb, IP-nkénti kérésszám-korláttal
            if (!string.IsNullOrEmpty(accessToken))
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            try
            {
                using var response = await client.SendAsync(message, cancellationToken);

                var failure = GitFetchErrorClassifier.Classify(response);
                if (failure != null) return (failure, null);

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                return (null, JsonDocument.Parse(body));
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                //Időtúllépés: a HttpClient ezt TaskCanceledException alakban jelzi
                return (GitFetchOutcome.Fail(GitFetchErrorKind.Transient,
                    "A szolgáltató nem válaszolt időben."), null);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "A GitHub API nem elérhető");
                return (GitFetchOutcome.Fail(GitFetchErrorKind.Transient,
                    "A szolgáltató nem elérhető."), null);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "A GitHub API válasza nem értelmezhető JSON");
                return (GitFetchOutcome.Fail(GitFetchErrorKind.Permanent,
                    "A szolgáltató válasza nem értelmezhető."), null);
            }
        }

        /// <summary>
        /// A compare végpont alakja NEM azonos a webhook payloadjával: a commit metaadata egy
        /// beágyazott objektumban van, a sha pedig "sha" és nem "id". Ezért van providerenként
        /// külön olvasás, a webhookos CommitArrayReader újrahasználása helyett.
        ///
        /// A visszatérés a lapon TALÁLT elemek száma, nem a sikeresen beolvasottaké: a lapozás
        /// vége abból derül ki, hogy a szolgáltató rövidebb lapot adott, és egy általunk
        /// kihagyott, hiányos commit nem jelenti az intervallum végét.
        /// </summary>
        private static int ReadCommits(JsonElement array, List<GitCommitInfo> target)
        {
            var seen = 0;

            foreach (var element in array.EnumerateArray())
            {
                seen++;

                if (!element.TryReadString("sha", out var sha) || sha.Length == 0) continue;
                if (!element.TryReadObject("commit", out var commit)) continue;
                if (!commit.TryReadString("message", out var message)) continue;

                var author = commit.TryReadObject("author", out var authorElement) ? authorElement : default;

                target.Add(new GitCommitInfo(
                    Sha: sha,
                    Message: message,
                    Url: element.ReadOptionalString("html_url"),
                    AuthorName: author.ReadOptionalString("name") ?? string.Empty,
                    AuthorEmail: author.ReadOptionalString("email") ?? string.Empty,

                    //Ugyanaz a megfontolás, mint a webhooknál: legalább egy közelítő idő
                    //kerüljön be, ne vesszen el a commit egy hiányzó időbélyeg miatt
                    CommittedAt: author.ReadTimestamp("date") ?? DateTime.UtcNow));
            }

            return seen;
        }
    }
}
