using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Data;
using ProjectManager.API.Hubs;
using ProjectManager.API.Model;
using ProjectManager.API.Services.ActivityService;
using ProjectManager.API.Services.GitWebhookService.Payloads;
using System.Security.Cryptography;
using System.Text;

namespace ProjectManager.API.Services.GitWebhookService
{
    public class GitWebhookService : IGitWebhookService
    {
        //A rövidített sha hossza az activity szövegében. A git maga is 7 karakterrel kezdi.
        private const int ShortShaLength = 7;

        //Ennyi karakter kerül a commit üzenetéből az activity szövegébe
        private const int MessagePreviewLength = 50;

        private readonly AppDbContext _context;
        private readonly IHubContext<ProjectHub> _hubContext;
        private readonly IActivityService _activityService;
        private readonly ILogger<GitWebhookService> _logger;

        public GitWebhookService(
            AppDbContext context,
            IHubContext<ProjectHub> hubContext,
            IActivityService activityService,
            ILogger<GitWebhookService> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _activityService = activityService;
            _logger = logger;
        }

        public async Task ProcessPullRequestEventAsync(
            Guid projectId, Guid integrationId, string provider, GitPullRequestEvent prEvent)
        {
            //Egy PR több taskhoz is illeszkedhet, tehát TÖBB sora lehet. Korábban a keresés
            //FirstOrDefault volt, így merge után csak az első sor kapta meg az új állapotot -
            //a többi task alatt "open" maradt a jelölés.
            var existingLinks = await _context.PrLinks
                .Where(pl => pl.IntegrationId == integrationId && pl.PrNumber == prEvent.Number)
                .ToListAsync();

            var stateChanged = existingLinks.Any(link => link.State != prEvent.State);

            foreach (var link in existingLinks)
            {
                link.State = prEvent.State;
                link.MergedAt = prEvent.MergedAt;
                link.Title = prEvent.Title;
            }

            //Az illesztés a címre és is leírásra fut. 
            var tasks = await MatchTasksAsync(
                projectId, TaskKeyMatcher.CombineTitleAndBody(prEvent.Title, prEvent.Description));

            var alreadyLinkedTaskIds = existingLinks
                .Where(link => link.TaskId.HasValue)
                .Select(link => link.TaskId!.Value)
                .ToHashSet();

            //Újraillesztés: a szerkesztés ága korábban korán visszatért,
            //tehát aki utólag írta bele a kulcsot a PR címébe vagy leírásába,
            //annál az összekapcsolás sosem jött létre.
            var newLinks = new List<(ProjectTask Task, PrLink Link)>();
            foreach (var task in tasks)
            {
                if (alreadyLinkedTaskIds.Contains(task.Id)) continue;

                var link = NewPrLink(task.Id, integrationId, prEvent);
                _context.PrLinks.Add(link);
                newLinks.Add((task, link));
            }

            //A hozzárendeletlen helyőrző sor felesleges,
            //amint van valódi találat - különben a PR egyszerre látszana a "hozzárendeletlen" listában és a task alatt
            var placeholder = existingLinks.FirstOrDefault(link => link.TaskId == null);
            if (placeholder != null && tasks.Count > 0)
            {
                _context.PrLinks.Remove(placeholder);
                existingLinks.Remove(placeholder);
            }

            PrLink? unmatchedLink = null;
            if (existingLinks.Count == 0 && tasks.Count == 0)
            {
                unmatchedLink = NewPrLink(null, integrationId, prEvent);
                _context.PrLinks.Add(unmatchedLink);
            }

            await _context.SaveChangesAsync();

            if (unmatchedLink != null)
            {
                await LogSystemActivityAsync(
                    projectId, "PullRequest", integrationId, "Unmatched",
                    $"Hozzárendeletlen PR érkezett: #{prEvent.Number} — {prEvent.Title} ({prEvent.AuthorName})");
            }

            foreach (var (task, link) in newLinks)
            {
                await BroadcastAsync(projectId, "PrLinked", PrLinkedPayload(task.Id, link));
                await LogPrStateActivityAsync(projectId, provider, task, link);
            }

            //Állapotváltozás a MÁR meglévő sorokon.
            //Enélkül a mentés megtörténik, de a böngésző nem tud róla:
            //a felhasználó egy mergelt PR-t "open" jelöléssel lát egészen az oldal újratöltéséig,
            //és a tevékenységlistába sem kerül semmi.
            if (!stateChanged) return;

            foreach (var link in existingLinks.Where(link => link.TaskId.HasValue))
            {
                var task = await _context.ProjectTasks
                    .FirstOrDefaultAsync(t => t.Id == link.TaskId!.Value);
                if (task == null) continue;

                await BroadcastAsync(projectId, "PrLinked", PrLinkedPayload(task.Id, link));
                await LogPrStateActivityAsync(projectId, provider, task, link);
            }
        }

        public async Task ProcessPushEventAsync(
            Guid projectId, Guid integrationId, string provider, GitPushEvent pushEvent)
        {
            var newLinks = new List<(ProjectTask Task, CommitLink Link)>();
            var unmatchedMessages = new List<string>();

            foreach (var commit in pushEvent.Commits)
            {
                //Egy commit üzenete több task kulcsát is tartalmazhatja,
                //tehát TÖBB sora lehet - ugyanaz a szerkezet, mint a pull requesteknél
                var existingLinks = await _context.CommitLinks
                    .Where(cl => cl.IntegrationId == integrationId && cl.CommitSha == commit.Sha)
                    .ToListAsync();

                //Forcepush: az üzenet és az URL felülíródhat ugyanazon a sha-n
                foreach (var link in existingLinks)
                {
                    link.Message = commit.Message;
                    link.CommitUrl = commit.Url;
                }

                var tasks = await MatchTasksAsync(projectId, commit.Message);

                var alreadyLinkedTaskIds = existingLinks
                    .Where(link => link.TaskId.HasValue)
                    .Select(link => link.TaskId!.Value)
                    .ToHashSet();

                foreach (var task in tasks)
                {
                    if (alreadyLinkedTaskIds.Contains(task.Id)) continue;

                    newLinks.Add((task, CreateCommitLink(task.Id, integrationId, commit)));
                }

                //A hozzárendeletlen helyőrző sor felesleges, amint van valódi találat
                var placeholder = existingLinks.FirstOrDefault(link => link.TaskId == null);
                if (placeholder != null && tasks.Count > 0)
                {
                    _context.CommitLinks.Remove(placeholder);
                    existingLinks.Remove(placeholder);
                }

                if (existingLinks.Count > 0 || tasks.Count > 0) continue;

                CreateCommitLink(null, integrationId, commit);
                unmatchedMessages.Add(
                    $"{ShortSha(commit.Sha)} — {Preview(commit.Message)} ({commit.AuthorName})");
            }

            await _context.SaveChangesAsync();

            foreach (var msg in unmatchedMessages)
            {
                await LogSystemActivityAsync(
                    projectId, "Commit", integrationId, "Unmatched",
                    $"Hozzárendeletlen commit érkezett: {msg}");
            }

            foreach (var (task, link) in newLinks)
            {
                await BroadcastAsync(projectId, "CommitLinked", CommitLinkedPayload(task.Id, link));

                await LogSystemActivityAsync(
                    projectId, "Commit", task.Id, "Linked",
                    $"{provider} kapcsolta a {ShortSha(link.CommitSha)} commitot a {task.TaskKey} taskhoz: {Preview(link.Message)}");
                    //A szolgáltató neve a payloadból jön: korábban itt "GitHub" volt beégetve
            }
        }

        public bool ValidateGitHubSignature(string payload, string signature, string secret)
        {
            var secretBytes = Encoding.UTF8.GetBytes(secret);
            var payloadBytes = Encoding.UTF8.GetBytes(payload);

            using var hmac = new HMACSHA256(secretBytes);
            var hash = hmac.ComputeHash(payloadBytes);
            var expectedSignature = "sha256=" + Convert.ToHexString(hash).ToLower();

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expectedSignature),
                Encoding.UTF8.GetBytes(signature)
            );
        }

        public bool ValidateGitLabSignature(string token, string secret)
        {
            if (string.IsNullOrEmpty(secret)) return false;
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(secret),
                Encoding.UTF8.GetBytes(token)
            );
        }

        private async Task<List<ProjectTask>> MatchTasksAsync(Guid projectId, string text)
        {
            var project = await _context.Projects
                .FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null) return [];

            var taskKeys = TaskKeyMatcher.ExtractTaskKeys(project.ProjKey, text);
            if (taskKeys.Count == 0) return [];

            return await _context.ProjectTasks
                .Where(t => t.ProjectId == projectId && taskKeys.Contains(t.TaskKey))
                .ToListAsync();
        }

        private static PrLink NewPrLink(Guid? taskId, Guid integrationId, GitPullRequestEvent prEvent) =>
            new()
            {
                Id = Guid.NewGuid(),
                TaskId = taskId,
                IntegrationId = integrationId,
                PrNumber = prEvent.Number,
                PrUrl = prEvent.Url,
                Title = prEvent.Title,
                State = prEvent.State,
                AuthorName = prEvent.AuthorName,
                CreatedAt = DateTime.UtcNow,
                MergedAt = prEvent.MergedAt
            };

        private CommitLink CreateCommitLink(Guid? taskId, Guid integrationId, GitCommitInfo commit)
        {
            var link = new CommitLink
            {
                Id = Guid.NewGuid(),
                TaskId = taskId,
                IntegrationId = integrationId,
                CommitSha = commit.Sha,
                CommitUrl = commit.Url,
                Message = commit.Message,
                AuthorName = commit.AuthorName,
                AuthorEmail = commit.AuthorEmail,
                CommittedAt = commit.CommittedAt
            };

            _context.CommitLinks.Add(link);
            return link;
        }

        /// <summary>
        /// A SignalR küldés sosem buktathatja el a webhook feldolgozását.
        /// </summary>
        private async Task BroadcastAsync(Guid projectId, string eventName, object payload)
        {
            try
            {
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync(eventName, payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                    eventName, projectId);
            }
        }

        private async Task LogSystemActivityAsync(
            Guid projectId, string entityType, Guid entityId, string action, string description)
        {
            try
            {
                var activity = await _activityService.LogSystemActivityAsync(
                    projectId, entityType, entityId, action, description);

                await BroadcastAsync(projectId, "ActivityCreated", activity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Activity naplózási hiba | EntityType: {EntityType} | ProjectId: {ProjectId}",
                    entityType, projectId);
            }
        }

        private Task LogPrStateActivityAsync(Guid projectId, string provider, ProjectTask task, PrLink link)
        {
            var (action, actionText) = link.State switch
            {
                GitPrStates.Merged => ("Merged", "mergelte"),
                GitPrStates.Closed => ("Closed", "lezárta"),
                _ => ("Opened", "megnyitotta")
            };

            return LogSystemActivityAsync(
                projectId, "PullRequest", task.Id, action,
                $"{provider} {actionText} a #{link.PrNumber} PR-t a {task.TaskKey} taskhoz: {link.Title}");
                //A szolgáltató neve a payloadból jön: korábban itt "GitHub" volt beégetve,
                //tehát egy GitLab merge requestről is azt írta volna ki
        }

        /// <summary>
        /// A SignalR payload alakja azonos a válasz DTO-jával, + taskId.
        /// Korábban a két forrás: a webhook és a kézi összekapcsolás esetén különböztek.
        /// </summary>
        private static object PrLinkedPayload(Guid taskId, PrLink link) => new
        {
            taskId,
            id = link.Id,
            prNumber = link.PrNumber,
            prUrl = link.PrUrl,
            title = link.Title,
            state = link.State,
            authorName = link.AuthorName,
            createdAt = link.CreatedAt,
            mergedAt = link.MergedAt
        };

        /// <inheritdoc cref="PrLinkedPayload"/>
        //Az AuthorEmail szándékosan nincs: harmadik személy adata, amire ma nem épül funkció
        private static object CommitLinkedPayload(Guid taskId, CommitLink link) => new
        {
            taskId,
            id = link.Id,
            commitSha = link.CommitSha,
            commitUrl = link.CommitUrl,
            message = link.Message,
            
            
            authorName = link.AuthorName,
            committedAt = link.CommittedAt
        };

        /// <summary>
        /// A sha eleje az activity szövegéhez. A hossz-ellenőrzés nem elméleti:
        /// egy csonka payloadból rövidebb azonosító is érkezhet, és a vágás akkor kivételt dobna.
        /// </summary>
        private static string ShortSha(string sha) =>
            sha.Length <= ShortShaLength ? sha : sha[..ShortShaLength];

        private static string Preview(string message) =>
            message.Length <= MessagePreviewLength ? message : message[..MessagePreviewLength];
    }
}
