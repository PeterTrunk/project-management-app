using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Data;
using ProjectManager.API.DTOs.Comments;
using ProjectManager.API.Hubs;
using ProjectManager.API.Model;
using ProjectManager.API.Services.CurrentUserService;

namespace ProjectManager.API.Services.CommentService
{
    public class CommentService : ICommentService
    {
        private readonly AppDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly IHubContext<ProjectHub> _hubContext;

        public CommentService(AppDbContext context, ICurrentUserService currentUserService, IHubContext<ProjectHub> hubContext)
        {
            _context = context;
            _currentUserService = currentUserService;
            _hubContext = hubContext;
        }

        public async Task<CommentResponseDto> CommentOnTaskAsync(Guid projectId, Guid taskId, CreateCommentDto dto)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található!");

            var task = await _context.ProjectTasks.FirstOrDefaultAsync(t => t.Id == taskId);
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
            await _hubContext.Clients
                .Group($"project-{projectId}")
                .SendAsync("CommentAdded", new
                {
                    taskId,
                    commentId = comment.Id,
                    body = comment.Body,
                    createdByName = user.DisplayName,
                    createdAt = comment.CreatedAt
                });

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

            var task = await _context.ProjectTasks.FirstOrDefaultAsync(t => t.Id == taskId);
            if (task == null)
                throw new Exception("Feladat nem található");

            var comment = await _context.Comments.FirstOrDefaultAsync(c => c.Id == commentId);
            if (comment == null)
                throw new Exception("Comment nem található");

            var callerId = _currentUserService.UserId;
            if (comment.UserId != callerId)
                throw new Exception("Csak a saját kommentedet törölheted!");

            _context.Comments.Remove(comment);

            await _context.SaveChangesAsync();
            await _hubContext.Clients
                .Group($"project-{projectId}")
                .SendAsync("CommentDeleted", new
                {
                    taskId,
                    commentId
                });
        }

        public async Task<List<CommentResponseDto>> GetCommentsAsync(Guid projectId, Guid taskId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található!");

            var task = await _context.ProjectTasks.FirstOrDefaultAsync(t => t.Id == taskId);
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
