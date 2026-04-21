using ProjectManager.API.Data;
using ProjectManager.API.DTOs.Activity;
using ProjectManager.API.Model;
using ProjectManager.API.Services.CurrentUserService;
using Microsoft.EntityFrameworkCore;

namespace ProjectManager.API.Services.ActivityService
{
    public class ActivityService : IActivityService
    {
        private readonly AppDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public ActivityService(AppDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<ActivityResponseDto> LogActivityAsync(Guid projectId, string entityType, Guid entityId, string action, string description, string? payload = null)
        {
            var activity = new Activity
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                ActorId = _currentUserService.UserId,
                EntityType = entityType,
                EntityId = entityId,
                Action = action,
                Description = description,
                Payload = payload,
                CreatedAt = DateTime.UtcNow
            };

            await _context.Activities.AddAsync(activity);
            await _context.SaveChangesAsync();

            var actor = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == activity.ActorId);

            return new ActivityResponseDto
            {
                Id = activity.Id,
                ActorName = actor?.DisplayName ?? "Ismeretlen",
                EntityType = activity.EntityType,
                EntityId = activity.EntityId,
                Action = activity.Action,
                Description = activity.Description,
                Payload = activity.Payload,
                CreatedAt = activity.CreatedAt
            };
        }

        public async Task<List<ActivityResponseDto>> GetActivitiesAsync(Guid projectId, int page = 1, int pageSize = 20)
        {
            var activities = await _context.Activities
                .Where(a => a.ProjectId == projectId)
                .Include(a => a.Actor)
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return activities.Select(a => new ActivityResponseDto
            {
                Id = a.Id,
                ActorName = a.Actor?.DisplayName ?? "Ismeretlen",
                EntityType = a.EntityType,
                EntityId = a.EntityId,
                Action = a.Action,
                Description = a.Description,
                Payload = a.Payload,
                CreatedAt = a.CreatedAt
            }).ToList();
        }
    }
}
