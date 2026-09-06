using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Common.Exceptions;
using ProjectManager.API.Data;
using ProjectManager.API.DTOs.Git;
using ProjectManager.API.Hubs;
using ProjectManager.API.Model;
using ProjectManager.API.Services.ActivityService;
using ProjectManager.API.Services.CurrentUserService;

namespace ProjectManager.API.Services.GitService
{
    public class GitService : IGitService
    {
        private readonly AppDbContext _context;
        private readonly IActivityService _activityService;
        private readonly IHubContext<ProjectHub> _hubContext;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<GitService> _logger;

        public GitService(
            AppDbContext context, 
            IActivityService activityService, 
            IHubContext<ProjectHub> hubContext, 
            ICurrentUserService currentUserService,
            ILogger<GitService> logger)
        {
            _context = context;
            _activityService = activityService;
            _hubContext = hubContext;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        public async Task<List<CommitLinkResponseDto>> GetUnmatchedCommitsAsync(Guid projectId)
        {
            return await _context.CommitLinks
                .Where(cl => cl.Integration.ProjectId == projectId && cl.TaskId == null)
                .OrderByDescending(cl => cl.CommittedAt)
                .Select(cl => new CommitLinkResponseDto
                {
                    Id = cl.Id,
                    CommitSha = cl.CommitSha,
                    CommitUrl = cl.CommitUrl,
                    Message = cl.Message,
                    AuthorName = cl.AuthorName,
                    AuthorEmail = cl.AuthorEmail,
                    CommittedAt = cl.CommittedAt
                })
                .ToListAsync();
        }

        public async Task<List<PrLinkResponseDto>> GetUnmatchedPrsAsync(Guid projectId)
        {
            return await _context.PrLinks
                .Where(pl => pl.Integration.ProjectId == projectId && pl.TaskId == null)
                .OrderByDescending(pl => pl.CreatedAt)
                .Select(pl => new PrLinkResponseDto
                {
                    Id = pl.Id,
                    PrNumber = pl.PrNumber,
                    PrUrl = pl.PrUrl,
                    Title = pl.Title,
                    State = pl.State,
                    AuthorName = pl.AuthorName,
                    CreatedAt = pl.CreatedAt,
                    MergedAt = pl.MergedAt
                })
                .ToListAsync();
        }

        public async Task AssignCommitToTaskAsync(Guid projectId, Guid commitId, Guid taskId)
        {
            var integrationIds = await _context.Integrations
                .Where(i => i.ProjectId == projectId)
                .Select(i => i.Id)
                .ToListAsync();

            var commit = await _context.CommitLinks
                .FirstOrDefaultAsync(cl =>
                    cl.Id == commitId &&
                    integrationIds.Contains(cl.IntegrationId));
            if (commit == null)
                throw new NotFoundException("Commit nem található!");

            var task = await _context.ProjectTasks
                .FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId);
            if (task == null)
                throw new NotFoundException("Task nem található!");

            commit.TaskId = taskId;
            await _context.SaveChangesAsync();

            try
            {
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("CommitLinked", new
                    {
                        taskId,
                        commitId = commit.Id,
                        commitSha = commit.CommitSha
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                    "CommitLinked", projectId);
            }

            try
            {
                var activity = await _activityService.LogActivityAsync(
                    projectId,
                    "Commit",
                    taskId,
                    "Linked",
                    $"{_currentUserService.DisplayName} manuálisan kapcsolta a {commit.CommitSha[..7]} commitot a taskhoz"
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

        public async Task AssignPrToTaskAsync(Guid projectId, Guid prId, Guid taskId)
        {
            var integrationIds = await _context.Integrations
                .Where(i => i.ProjectId == projectId)
                .Select(i => i.Id)
                .ToListAsync();

            var pr = await _context.PrLinks
                .FirstOrDefaultAsync(pl =>
                    pl.Id == prId &&
                    integrationIds.Contains(pl.IntegrationId));
            if (pr == null)
                throw new NotFoundException("PR nem található!");

            var task = await _context.ProjectTasks
                .FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId);
            if (task == null)
                throw new NotFoundException("Task nem található!");

            pr.TaskId = taskId;
            await _context.SaveChangesAsync();

            try
            {
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("PrLinked", new
                    {
                        taskId,
                        prId = pr.Id,
                        prNumber = pr.PrNumber
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                    "PrLinked", projectId);
            }

            try
            {
                var activity = await _activityService.LogActivityAsync(
                    projectId,
                    "PullRequest",
                    taskId,
                    "Linked",
                    $"{_currentUserService.DisplayName} manuálisan kapcsolta a #{pr.PrNumber} PR-t a taskhoz"
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
}
