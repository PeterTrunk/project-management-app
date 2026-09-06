using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Data;
using ProjectManager.API.DTOs.Comments;
using ProjectManager.API.Hubs;
using ProjectManager.API.Model;
using ProjectManager.API.Services.ActivityService;
using ProjectManager.API.Services.CurrentUserService;

namespace ProjectManager.API.Services.CommentService
{
    public class CommentService : ICommentService
    {
        private readonly AppDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly IHubContext<ProjectHub> _hubContext;
        private readonly IActivityService _activityService;
        private readonly ILogger<CommentService> _logger;

        public CommentService(
            AppDbContext context, 
            ICurrentUserService currentUserService, 
            IHubContext<ProjectHub> hubContext, 
            IActivityService activityService,
            ILogger<CommentService> logger)
        {
            _context = context;
            _currentUserService = currentUserService;
            _hubContext = hubContext;
            _activityService = activityService;
            _logger = logger;
        }

        public async Task<CommentResponseDto> CommentOnTaskAsync(Guid projectId, Guid taskId, CreateCommentDto dto)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található!");

            var task = await _context.ProjectTasks
                .FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId);
            if (task == null)
                throw new Exception("Feladat nem található");

            var userId = _currentUserService.UserId;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                throw new Exception("Felhasználó nem található!");


            var comment = new Comment
            {
                TaskId = taskId,
                UserId = user.Id,
                Body = dto.Body
            };
            _context.Comments.Add(comment);

            await _context.SaveChangesAsync();

            try
            {
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("CommentAdded", new
                    {
                        taskId,
                        commentId = comment.Id,
                        body = comment.Body,
                        createdById = comment.UserId,
                        createdByName = user.DisplayName,
                        createdAt = comment.CreatedAt,
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                    "CommentAdded", projectId);
            }

            try
            {
                var activity = await _activityService.LogActivityAsync(
                    projectId,
                    "Comment",
                    comment.Id,
                    "Created",
                    $"{_currentUserService.DisplayName} kommentelt a {task.TaskKey} taskon"
                );
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("ActivityCreated", activity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: ActivityCreated | ProjectId: {ProjectId}", projectId);
            }

            var response = new CommentResponseDto
            {
                Id = comment.Id,
                TaskId = comment.TaskId,
                UserId = comment.UserId,
                UserName = user.DisplayName,
                Body = comment.Body,
                CreatedAt = comment.CreatedAt,
                UpdatedAt = comment.UpdatedAt
            };
            return response;
        }

        public async Task DeleteCommentFromTaskAsync(Guid projectId, Guid taskId, Guid commentId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található!");

            var task = await _context.ProjectTasks
                .FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId);
            if (task == null)
                throw new Exception("Feladat nem található");

            //A komment a most már projektre szűrt taskhoz kell tartozzon, különben
            //idegen projekt kommentje is törölhető
            var comment = await _context.Comments
                .FirstOrDefaultAsync(c => c.Id == commentId && c.TaskId == taskId);
            if (comment == null)
                throw new Exception("Comment nem található");

            var callerId = _currentUserService.UserId;
            if (comment.UserId != callerId)
                throw new Exception("Csak a saját kommentedet törölheted!");

            _context.Comments.Remove(comment);

            await _context.SaveChangesAsync();

            try
            {
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("CommentDeleted", new
                    {
                        taskId,
                        commentId
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                    "CommentDeleted", projectId);
            }

            try
            {
                var activity = await _activityService.LogActivityAsync(
                    projectId,
                    "Comment",
                    commentId,
                    "Deleted",
                    $"{_currentUserService.DisplayName} törölt egy kommentet a {task.TaskKey} taskon"
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

        public async Task<List<CommentResponseDto>> GetCommentsAsync(Guid projectId, Guid taskId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található!");

            var task = await _context.ProjectTasks
                .FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId);
            if (task == null)
                throw new Exception("Feladat nem található");

            var comments = await _context.Comments
                .Where(c => c.TaskId == taskId)
                .Include(c => c.User)
                .ToListAsync();

            return comments.Select(c => new CommentResponseDto
            {
                Id = c.Id,
                TaskId = c.TaskId,
                UserId = c.UserId,
                UserName = c.User.DisplayName,
                Body = c.Body,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt
            }).ToList();
        }
    }
}
