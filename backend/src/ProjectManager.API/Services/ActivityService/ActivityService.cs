using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Data;
using ProjectManager.API.DTOs.Activity;
using ProjectManager.API.Model;
using ProjectManager.API.Services.CurrentUserService;

namespace ProjectManager.API.Services.ActivityService
{
    public class ActivityService : IActivityService
    {
        private readonly AppDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        //A leírások sablonként tárolódnak, a személyneveket ezek a jelölők helyettesítik.
        //Csak a neveket: a board és tasknevek beégetve maradnak, mert nem személyes adatok.
        public const string ActorPlaceholder = "{actor}";
        public const string TargetPlaceholder = "{target}";

        private const string UnknownName = "Ismeretlen";
        private const string SystemName = "System";

        public ActivityService(AppDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        /// <summary>
        /// A sablonban lévő jelölőket kicseréli a hivatkozott felhasználók AKTUÁLIS nevére.
        ///
        /// A jelölők bevezetése előtt keletkezett sorok kész szöveget tartalmaznak: azokban
        /// nincs mit cserélni, ezért változatlanul mennek tovább.
        /// </summary>
        private static string Render(string template, string? actorName, string? targetName) =>
            template
                .Replace(ActorPlaceholder, actorName ?? UnknownName)
                .Replace(TargetPlaceholder, targetName ?? UnknownName);

        public async Task<ActivityResponseDto> LogActivityAsync(
            Guid projectId,
            string entityType,
            Guid entityId,
            string action,
            string description,
            string? payload = null,
            Guid? targetUserId = null)
        {
            var activity = new Activity
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                ActorId = _currentUserService.UserId,
                TargetUserId = targetUserId,
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

            //Csak akkor kérdezünk le, ha van kire: a leírások többsége nem irányul senkire
            var target = targetUserId.HasValue
                ? await _context.Users.FirstOrDefaultAsync(u => u.Id == targetUserId.Value)
                : null;

            return new ActivityResponseDto
            {
                Id = activity.Id,
                ActorName = actor?.DisplayName ?? UnknownName,
                TargetName = target?.DisplayName,
                EntityType = activity.EntityType,
                EntityId = activity.EntityId,
                Action = activity.Action,
                Description = Render(activity.Description, actor?.DisplayName, target?.DisplayName),
                Payload = activity.Payload,
                CreatedAt = activity.CreatedAt
            };
        }

        public async Task<List<ActivityResponseDto>> GetActivitiesAsync(
            Guid projectId,
            int page = 1,
            int pageSize = 20,
            string? entityType = null,
            string? actorName = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null)
        {
            var query = _context.Activities
                .Where(a => a.ProjectId == projectId)
                .Include(a => a.Actor)
                .Include(a => a.TargetUser)
                .AsQueryable();

            // EntityType szűrés
            if (!string.IsNullOrEmpty(entityType))
                query = query.Where(a => a.EntityType == entityType);

            // ActorName szűrés
            if (!string.IsNullOrEmpty(actorName))
                query = query.Where(a =>
                    a.Actor != null &&
                    a.Actor.DisplayName.ToLower().Contains(actorName.ToLower()));

            // Dátum szűrés
            if (dateFrom.HasValue)
                query = query.Where(a => a.CreatedAt >= DateTime.SpecifyKind(dateFrom.Value, DateTimeKind.Utc));

            if (dateTo.HasValue)
                query = query.Where(a => a.CreatedAt <= DateTime.SpecifyKind(dateTo.Value, DateTimeKind.Utc));

            var activities = await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return activities.Select(a =>
            {
                var resolvedActorName = a.ActorId.HasValue
                    ? (a.Actor?.DisplayName ?? UnknownName) //Ha nem találjuk a usert akkor "Ismeretlen"
                    : SystemName;                           //null ActorId esetén "System"

                return new ActivityResponseDto
                {
                    Id = a.Id,
                    ActorName = resolvedActorName,
                    TargetName = a.TargetUser?.DisplayName,
                    EntityType = a.EntityType,
                    EntityId = a.EntityId,
                    Action = a.Action,
                    Description = Render(a.Description, resolvedActorName, a.TargetUser?.DisplayName),
                    Payload = a.Payload,
                    CreatedAt = a.CreatedAt
                };
            }).ToList();
        }

        public async Task<ActivityResponseDto> LogSystemActivityAsync(Guid projectId, string entityType, Guid entityId, string action, string description, string? payload = null)
        {
            var activity = new Activity
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                ActorId = null, //System event. pl.: Push vagy Pr webhook érzékelés esetén
                EntityType = entityType,
                EntityId = entityId,
                Action = action,
                Description = description,
                Payload = payload,
                CreatedAt = DateTime.UtcNow
            };

            await _context.Activities.AddAsync(activity);
            await _context.SaveChangesAsync();

            return new ActivityResponseDto
            {
                Id = activity.Id,
                ActorName = SystemName,
                EntityType = activity.EntityType,
                EntityId = activity.EntityId,
                Action = activity.Action,
                //A rendszeresemények ma nem használnak jelölőt, de a futtatása így is helyes
                Description = Render(activity.Description, SystemName, null),
                Payload = activity.Payload,
                CreatedAt = activity.CreatedAt
            };
        }
    }
}
