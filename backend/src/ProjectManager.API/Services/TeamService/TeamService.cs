using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Data;
using ProjectManager.API.DTOs.Team;
using ProjectManager.API.Hubs;
using ProjectManager.API.Model;
using ProjectManager.API.Services.ActivityService;
using ProjectManager.API.Services.CurrentUserService;

namespace ProjectManager.API.Services.TeamService
{
    public class TeamService : ITeamService
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<ProjectHub> _hubContext;
        private readonly ICurrentUserService _currentUserService;
        private readonly IActivityService _activityService;

        public TeamService(AppDbContext context, IHubContext<ProjectHub> hubContext, ICurrentUserService currentUserService, IActivityService activityService)
        {
            _context = context;
            _hubContext = hubContext;
            _currentUserService = currentUserService;
            _activityService = activityService;
        }

        public async Task<InviteLinkResponseDto> GenerateInviteLinkAsync(Guid projectId, GenerateInviteLinkDto dto)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található!");

            // 32 karakter, kötőjel nélkül
            var token = Guid.NewGuid().ToString("N");

            var invite = new ProjectInvite
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                CreatedById = _currentUserService.UserId,
                Token = token,
                ExpiresAt = dto.ExpiresInDays.HasValue
                    ? DateTime.UtcNow.AddDays(dto.ExpiresInDays.Value)
                    : DateTime.MaxValue,  // végtelen
                MaxUses = dto.MaxUses,
                UseCount = 0,
                CreatedAt = DateTime.UtcNow
            };

            await _context.ProjectInvites.AddAsync(invite);
            await _context.SaveChangesAsync();

            return new InviteLinkResponseDto
            {
                Token = token,
                ExpiresAt = invite.ExpiresAt,
                MaxUses = invite.MaxUses,
                UseCount = invite.UseCount,
                InviteUrl = $"http://localhost:5173/#/invite/{token}"
            };
        }

        public async Task<List<ProjectMemberResponseDto>> GetMembersAsync(Guid projectId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található!");

            var members = await _context.ProjectMembers
                .Where(pm => pm.ProjectId == projectId)
                .Include(pm => pm.User)
                .ToListAsync();

            return members.Select(pm => new ProjectMemberResponseDto
            {
                UserId = pm.UserId,
                DisplayName = pm.User.DisplayName,
                Email = pm.User.Email,
                ProjectRole = pm.ProjectRole,
                JoinedAt = pm.JoinedAt
            }).ToList();
        }

        public async Task<ProjectMemberResponseDto> JoinProjectAsync(string token)
        {
            var invite = await _context.ProjectInvites
                .Include(i => i.Project)
                .FirstOrDefaultAsync(i => i.Token == token);

            if (invite == null)
                throw new Exception("Érvénytelen meghívó link!");

            // Lejárt?
            if (invite.ExpiresAt < DateTime.UtcNow)
                throw new Exception("A meghívó link lejárt!");

            // MaxUses elérve?
            if (invite.MaxUses.HasValue && invite.UseCount >= invite.MaxUses)
                throw new Exception("A meghívó link maximális használati száma elérve!");

            var callerId = _currentUserService.UserId;

            // Már tagja?
            var existingMember = await _context.ProjectMembers
                .FirstOrDefaultAsync(pm => pm.ProjectId == invite.ProjectId && pm.UserId == callerId);
            if (existingMember != null)
                throw new Exception("Már tagja vagy ennek a projektnek!");

            // Hozzáadás Member szerepkörrel
            var member = new ProjectMember
            {
                ProjectId = invite.ProjectId,
                UserId = callerId,
                ProjectRole = "Member",
                JoinedAt = DateTime.UtcNow
            };

            await _context.ProjectMembers.AddAsync(member);

            // UseCount növelése
            invite.UseCount++;

            await _context.SaveChangesAsync();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == callerId);

            await _hubContext.Clients
                .Group($"project-{invite.ProjectId}")
                .SendAsync("MemberAdded", new
                {
                    userId = callerId,
                    displayName = user?.DisplayName,
                    projectRole = "Member"
                });

            try
            {
                var activity = await _activityService.LogActivityAsync(
                    invite.ProjectId,
                    "Member",
                    callerId,
                    "Joined",
                    $"{user?.DisplayName} csatlakozott a projekthez"
                );
                await _hubContext.Clients
                    .Group($"project-{invite.ProjectId}")
                    .SendAsync("ActivityCreated", activity);
            }
            catch { }

            return new ProjectMemberResponseDto
            {
                UserId = callerId,
                DisplayName = user?.DisplayName ?? "",
                Email = user?.Email ?? "",
                ProjectRole = "Member",
                JoinedAt = member.JoinedAt
            };
        }

        public async Task RemoveMemberAsync(Guid projectId, Guid userId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található!");

            // Owner nem távolítható el
            if (project.OwnerId == userId)
                throw new Exception("A projekt tulajdonosa nem távolítható el!");

            // Saját magát sem távolíthatja el a hívó
            var callerId = _currentUserService.UserId;
            if (callerId == userId)
                throw new Exception("Saját magad nem távolíthatod el!");

            var member = await _context.ProjectMembers
                .FirstOrDefaultAsync(pm => pm.ProjectId == projectId && pm.UserId == userId);
            if (member == null)
                throw new Exception("A felhasználó nem tagja a projektnek!");

            _context.ProjectMembers.Remove(member);

            await _context.SaveChangesAsync();
            await _hubContext.Clients
                .Group($"project-{projectId}")
                .SendAsync("MemberRemoved", new { userId });

            try
            {
                var removedUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                var activity = await _activityService.LogActivityAsync(
                    projectId,
                    "Member",
                    userId,
                    "Removed",
                    $"{_currentUserService.DisplayName} eltávolította {removedUser?.DisplayName}-t a projektből"
                );
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("ActivityCreated", activity);
            }
            catch { }
        }

        public async Task<ProjectMemberResponseDto> UpdateMemberRoleAsync(Guid projectId, Guid userId, UpdateMemberRoleDto dto)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található!");

            // Owner szerepköre nem módosítható
            if (project.OwnerId == userId)
                throw new Exception("A projekt tulajdonosának szerepköre nem módosítható!");

            var member = await _context.ProjectMembers
                .Include(pm => pm.User)
                .FirstOrDefaultAsync(pm => pm.ProjectId == projectId && pm.UserId == userId);
            if (member == null)
                throw new Exception("A felhasználó nem tagja a projektnek!");

            member.ProjectRole = dto.ProjectRole;
            await _context.SaveChangesAsync();

            await _hubContext.Clients
                .Group($"project-{projectId}")
                .SendAsync("MemberRoleUpdated", new
                {
                    userId,
                    projectRole = dto.ProjectRole
                });

            try
            {
                var activity = await _activityService.LogActivityAsync(
                    projectId,
                    "Member",
                    userId,
                    "RoleUpdated",
                    $"{_currentUserService.DisplayName} módosította {member.User.DisplayName} szerepkörét {dto.ProjectRole}-re"
                );
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("ActivityCreated", activity);
            }
            catch { }

            return new ProjectMemberResponseDto
            {
                UserId = member.UserId,
                DisplayName = member.User.DisplayName,
                Email = member.User.Email,
                ProjectRole = member.ProjectRole,
                JoinedAt = member.JoinedAt
            };
        }
    }
}
