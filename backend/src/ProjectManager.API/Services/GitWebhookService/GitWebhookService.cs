using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Data;
using ProjectManager.API.Hubs;
using ProjectManager.API.Model;
using ProjectManager.API.Services.ActivityService;
using ProjectManager.API.Services.GitWebhookService.Payloads;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

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
            var matchedTasks = new List<ProjectTask>();

            // Létező PR frissítése vagy új létrehozása
            var existingPr = await _context.PrLinks
                .FirstOrDefaultAsync(pl =>
                    pl.IntegrationId == integrationId &&
                    pl.PrNumber == prEvent.Number);

            bool isUnmatched = false;

            if (existingPr != null)
            {
                existingPr.State = prEvent.State;
                existingPr.MergedAt = prEvent.MergedAt;
                existingPr.Title = prEvent.Title;

                // Ha csak title változott (edited action), nincs más változás
                await _context.SaveChangesAsync();
                return;
            }
            else
            {
                // Task matching
                var tasks = await MatchTasksAsync(projectId, prEvent.Title);

                if (tasks.Count == 0)
                {
                    isUnmatched = true;
                    // Unmatched PR
                    _context.PrLinks.Add(NewPrLink(null, integrationId, prEvent));
                }
                else
                {
                    foreach (var task in tasks)
                    {
                        matchedTasks.Add(task);
                        _context.PrLinks.Add(NewPrLink(task.Id, integrationId, prEvent));
                    }
                }
            }

            await _context.SaveChangesAsync();

            // Unmatched PR logolás
            if (isUnmatched)
            {
                try
                {
                    var activity = await _activityService.LogSystemActivityAsync(
                        projectId,
                        "PullRequest",
                        integrationId,
                        "Unmatched",
                        $"Hozzárendeletlen PR érkezett: #{prEvent.Number} — {prEvent.Title} ({prEvent.AuthorName})"
                    );
                    await _hubContext.Clients
                        .Group($"project-{projectId}")
                        .SendAsync("ActivityCreated", activity);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SignalR broadcast hiba | Event: ActivityCreated | ProjectId: {ProjectId}", projectId);
                }
            }

            foreach (var task in matchedTasks)
            {
                try
                {
                    await _hubContext.Clients
                        .Group($"project-{projectId}")
                        .SendAsync("PrLinked", new
                        {
                            taskId = task.Id,
                            prNumber = prEvent.Number,
                            title = prEvent.Title,
                            state = prEvent.State,
                            authorName = prEvent.AuthorName
                        });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                        "PrLinked", projectId);
                }

                try
                {
                    var actionText = prEvent.State switch
                    {
                        GitPrStates.Merged => "mergelte",
                        GitPrStates.Closed => "lezárta",
                        _ => "megnyitotta"
                    };

                    var activity = await _activityService.LogSystemActivityAsync(
                        projectId,
                        "PullRequest",
                        task.Id,
                        prEvent.State == GitPrStates.Merged ? "Merged"
                            : prEvent.State == GitPrStates.Closed ? "Closed"
                            : "Opened",
                        $"{provider} {actionText} a #{prEvent.Number} PR-t a {task.TaskKey} taskhoz: {prEvent.Title}"
                    );
                    await _hubContext.Clients
                        .Group($"project-{projectId}")
                        .SendAsync("ActivityCreated", activity);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SignalR broadcast hiba | Event: ActivityCreated | ProjectId: {ProjectId}", projectId);
                }
            }
        }

        public async Task ProcessPushEventAsync(
            Guid projectId, Guid integrationId, string provider, GitPushEvent pushEvent)
        {
            var matchedTasks = new List<(ProjectTask task, string sha, string message, string authorName)>();
            var unmatchedMessages = new List<string>();

            foreach (var commit in pushEvent.Commits)
            {
                // Task matching
                var tasks = await MatchTasksAsync(projectId, commit.Message);

                if (tasks.Count == 0)
                {
                    // Unmatched commit — ellenőrzés hogy már létezik-e
                    var existingUnmatched = await _context.CommitLinks
                        .FirstOrDefaultAsync(cl =>
                            cl.IntegrationId == integrationId &&
                            cl.CommitSha == commit.Sha);
                    if (existingUnmatched != null) continue;

                    CreateCommitLink(null, integrationId, commit);

                    unmatchedMessages.Add(
                        $"{ShortSha(commit.Sha)} — {Preview(commit.Message)} ({commit.AuthorName})");
                }
                else
                {
                    foreach (var task in tasks)
                    {
                        // Már létezik?
                        var existing = await _context.CommitLinks
                            .FirstOrDefaultAsync(cl =>
                                cl.IntegrationId == integrationId &&
                                cl.CommitSha == commit.Sha &&
                                cl.TaskId == task.Id);
                        if (existing != null)
                        {
                            //Forcepush: üzenet és URL frissítése.
                            existing.Message = commit.Message;
                            existing.CommitUrl = commit.Url;

                            continue;
                        }

                        CreateCommitLink(task.Id, integrationId, commit);
                    }

                    foreach (var task in tasks)
                    {
                        matchedTasks.Add((task, commit.Sha, commit.Message, commit.AuthorName));
                    }
                }
            }

            await _context.SaveChangesAsync();

            foreach (var msg in unmatchedMessages)
            {
                try
                {
                    var activity = await _activityService.LogSystemActivityAsync(
                        projectId, "Commit", integrationId, "Unmatched",
                        $"Hozzárendeletlen commit érkezett: {msg}"
                    );
                    await _hubContext.Clients
                        .Group($"project-{projectId}")
                        .SendAsync("ActivityCreated", activity);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SignalR broadcast hiba | Event: ActivityCreated | ProjectId: {ProjectId}", projectId);
                }
            }

            foreach (var (task, sha, message, authorName) in matchedTasks)
            {
                try
                {
                    await _hubContext.Clients
                        .Group($"project-{projectId}")
                        .SendAsync("CommitLinked", new { taskId = task.Id, sha, message, authorName });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                        "CommitLinked", projectId);
                }

                try
                {
                    var activity = await _activityService.LogSystemActivityAsync(
                        projectId,
                        "Commit",
                        task.Id,
                        "Linked",
                        $"{provider} kapcsolta a {ShortSha(sha)} commitot a {task.TaskKey} taskhoz: {Preview(message)}"
                    );
                    await _hubContext.Clients
                        .Group($"project-{projectId}")
                        .SendAsync("ActivityCreated", activity);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SignalR broadcast hiba | Event: ActivityCreated | ProjectId: {ProjectId}", projectId);
                }
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
            //Projekt ProjKey lekérése
            var project = await _context.Projects
                .FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null) return new List<ProjectTask>();

            //Valid Regexek: PM-123, #PM-123, [PM-123], (PM-123)
            //A ProjKey escape-elve kerül a mintába: a validátor ma ugyan csak [A-Z0-9]-t enged,
            //de egyetlen lazítása regex-injektálást nyitna egy webhook payloadon futó illesztésben. A timeout a ReDoS ellen véd.
            var pattern = $@"(?:^|[\s\[(\#])({Regex.Escape(project.ProjKey)}-\d+)(?:$|[\s\])\.,!])";
            var matches = Regex.Matches(
                text, pattern,
                RegexOptions.IgnoreCase,
                TimeSpan.FromMilliseconds(100));

            var taskKeys = matches
                .Select(m => m.Groups[1].Value.ToUpper())
                .Distinct()
                .ToList();

            if (!taskKeys.Any()) return new List<ProjectTask>();

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

        private void CreateCommitLink(Guid? taskId, Guid integrationId, GitCommitInfo commit)
        {
            _context.CommitLinks.Add(new CommitLink
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
            });
        }

        private static string ShortSha(string sha) =>
            sha.Length <= ShortShaLength ? sha : sha[..ShortShaLength];

        private static string Preview(string message) =>
            message.Length <= MessagePreviewLength ? message : message[..MessagePreviewLength];
    }
}
