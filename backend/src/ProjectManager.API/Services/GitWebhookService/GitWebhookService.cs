using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Data;
using ProjectManager.API.Hubs;
using ProjectManager.API.Model;
using ProjectManager.API.Services.ActivityService;
using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ProjectManager.API.Services.GitWebhookService
{
    public class GitWebhookService : IGitWebhookService
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<ProjectHub> _hubContext;
        private readonly IActivityService _activityService;

        public GitWebhookService(AppDbContext context, IHubContext<ProjectHub> hubContext, IActivityService activityService)
        {
            _context = context;
            _hubContext = hubContext;
            _activityService = activityService;
        }

        public async Task ProcessPullRequestEventAsync(Guid projectId, Guid integrationId, JsonElement payload)
        {
            var action = payload.GetProperty("action").GetString();
            var pr = payload.GetProperty("pull_request");
            
            var prNumber = pr.GetProperty("number").GetInt32();
            var title = pr.GetProperty("title").GetString() ?? string.Empty;
            var prUrl = pr.TryGetProperty("html_url", out var urlProp)
                ? urlProp.GetString() : null;
            var authorName = pr.GetProperty("user")
                .GetProperty("login").GetString() ?? string.Empty;
            var repoFullName = payload.GetProperty("repository")
                .GetProperty("full_name").GetString() ?? string.Empty;
            
            var matchedTasks = new List<ProjectTask>();

            // State meghatározása
            var state = action switch
            {
                "opened" or "reopened" => "open",
                "closed" => pr.TryGetProperty("merged", out var merged) &&
                             merged.GetBoolean() ? "merged" : "closed",
                _ => "open"
            };

            // Csak ezeket kezeljük
            if (action != "opened" &&
                action != "closed" &&
                action != "reopened" &&
                action != "edited")
            {
                return;  // ignoráljuk a többi action-t
            }
            
            DateTime? mergedAt = null;
            if (state == "merged" && pr.TryGetProperty("merged_at", out var mergedAtProp))
            {
                mergedAt = DateTime.Parse(mergedAtProp.GetString()!).ToUniversalTime();
            }

            // Létező PR frissítése vagy új létrehozása
            var existingPr = await _context.PrLinks
                .FirstOrDefaultAsync(pl =>
                    pl.IntegrationId == integrationId &&
                    pl.PrNumber == prNumber);

            bool isUnmatched = false;

            if (existingPr != null)
            {
                existingPr.State = state;
                existingPr.MergedAt = mergedAt;
                existingPr.Title = title;

                // Ha csak title változott (edited action), nincs más változás
                await _context.SaveChangesAsync();
                return;
            }
            else
            {
                // Task matching
                var tasks = await MatchTasksAsync(projectId, title);

                if (tasks.Count == 0)
                {
                    isUnmatched = true;
                    // Unmatched PR
                    _context.PrLinks.Add(new PrLink
                    {
                        Id = Guid.NewGuid(),
                        TaskId = null,
                        IntegrationId = integrationId,
                        PrNumber = prNumber,
                        PrUrl = prUrl,
                        Title = title,
                        State = state,
                        AuthorName = authorName,
                        CreatedAt = DateTime.UtcNow,
                        MergedAt = mergedAt
                    });
                }
                else
                {
                    foreach (var task in tasks)
                    {
                        matchedTasks.Add(task);
                        _context.PrLinks.Add(new PrLink
                        {
                            Id = Guid.NewGuid(),
                            TaskId = task.Id,
                            IntegrationId = integrationId,
                            PrNumber = prNumber,
                            PrUrl = prUrl,
                            Title = title,
                            State = state,
                            AuthorName = authorName,
                            CreatedAt = DateTime.UtcNow,
                            MergedAt = mergedAt
                        });
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
                        $"Hozzárendeletlen PR érkezett: #{prNumber} — {title} ({authorName})"
                    );
                    await _hubContext.Clients
                        .Group($"project-{projectId}")
                        .SendAsync("ActivityCreated", activity);
                }
                catch { }
            }

            foreach (var task in matchedTasks)
            {
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("PrLinked", new { taskId = task.Id, prNumber, title, state, authorName });

                try
                {
                    var actionText = state switch
                    {
                        "merged" => "mergelte",
                        "closed" => "lezárta",
                        _ => "megnyitotta"
                    };

                    var activity = await _activityService.LogSystemActivityAsync(
                        projectId,
                        "PullRequest",
                        task.Id,
                        state == "merged" ? "Merged" : state == "closed" ? "Closed" : "Opened",
                        $"GitHub {actionText} a #{prNumber} PR-t a {task.TaskKey} taskhoz: {title}"
                    );
                    await _hubContext.Clients
                        .Group($"project-{projectId}")
                        .SendAsync("ActivityCreated", activity);
                }
                catch { }
            }
        }

        public async Task ProcessPushEventAsync(Guid projectId, Guid integrationId, JsonElement payload)
        {
            var commits = payload.GetProperty("commits");
            var matchedTasks = new List<(ProjectTask task, string sha, string message, string authorName)>();
            var unmatchedMessages = new List<string>();

            foreach (var commit in commits.EnumerateArray())
            {
                var sha = commit.GetProperty("id").GetString() ?? string.Empty;
                var message = commit.GetProperty("message").GetString() ?? string.Empty;
                var url = commit.TryGetProperty("url", out var urlProp)
                    ? urlProp.GetString() : null;
                var authorName = commit.GetProperty("author")
                    .GetProperty("name").GetString() ?? string.Empty;
                var authorEmail = commit.GetProperty("author")
                    .GetProperty("email").GetString() ?? string.Empty;
                var committedAt = commit.TryGetProperty("timestamp", out var tsProp)
                    ? DateTime.Parse(tsProp.GetString()!).ToUniversalTime()
                    : DateTime.UtcNow;

                // Task matching
                var tasks = await MatchTasksAsync(projectId, message);
                

                if (tasks.Count == 0)
                {
                    // Unmatched commit — ellenőrzés hogy már létezik-e
                    var existingUnmatched = await _context.CommitLinks
                        .FirstOrDefaultAsync(cl =>
                            cl.IntegrationId == integrationId &&
                            cl.CommitSha == sha);
                    if (existingUnmatched != null) continue;

                    await CreateCommitLinkAsync(
                        null, integrationId, sha, url, message,
                        authorName, authorEmail, committedAt);

                    unmatchedMessages.Add($"{sha[..7]} — {message[..Math.Min(50, message.Length)]} ({authorName})");
                }
                else
                {
                    foreach (var task in tasks)
                    {
                        // Már létezik?
                        var existing = await _context.CommitLinks
                            .FirstOrDefaultAsync(cl =>
                                cl.IntegrationId == integrationId &&
                                cl.CommitSha == sha &&
                                cl.TaskId == task.Id);
                        if (existing != null)
                        {
                            //Forcepush: üzenet és URL frissítése.
                            existing.Message = message;
                            existing.CommitUrl = url;

                            continue;
                        }

                        await CreateCommitLinkAsync(
                            task.Id, integrationId, sha, url, message,
                            authorName, authorEmail, committedAt);
                    }
                }
                
                if (tasks.Count > 0)
                {
                    foreach (var task in tasks)
                    {
                        matchedTasks.Add((task, sha, message, authorName));
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
                catch { }
            }

            foreach (var (task, sha, message, authorName) in matchedTasks)
            {
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("CommitLinked", new { taskId = task.Id, sha, message, authorName });

                try
                {
                    var activity = await _activityService.LogSystemActivityAsync(
                        projectId,
                        "Commit",
                        task.Id,
                        "Linked",
                        $"GitHub kapcsolta a {sha[..7]} commitot a {task.TaskKey} taskhoz: {message[..Math.Min(50, message.Length)]}"
                    );
                    await _hubContext.Clients
                        .Group($"project-{projectId}")
                        .SendAsync("ActivityCreated", activity);
                }
                catch { }
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
            var pattern = $@"(?:^|[\s\[(\#])({project.ProjKey}-\d+)(?:$|[\s\])\.,!])";
            var matches = System.Text.RegularExpressions.Regex.Matches(
                text, pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            var taskKeys = matches
                .Select(m => m.Groups[1].Value.ToUpper())
                .Distinct()
                .ToList();

            if (!taskKeys.Any()) return new List<ProjectTask>();

            return await _context.ProjectTasks
                .Where(t => t.ProjectId == projectId && taskKeys.Contains(t.TaskKey))
                .ToListAsync();
        }

        private async Task CreateCommitLinkAsync(
            Guid? taskId, Guid integrationId,
            string sha, string? url, string message, string authorName, string authorEmail,
            DateTime committedAt)
        {
            _context.CommitLinks.Add(new CommitLink
            {
                Id = Guid.NewGuid(),
                TaskId = taskId,
                IntegrationId = integrationId,
                CommitSha = sha,
                CommitUrl = url,
                Message = message,
                AuthorName = authorName,
                AuthorEmail = authorEmail,
                CommittedAt = committedAt
            });
        }
    }
}
