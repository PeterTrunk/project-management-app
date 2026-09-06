using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Authorization.Requirements;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.Data;
using System.Security.Claims;

namespace ProjectManager.API.Authorization.Handlers
{
    public class ProjectRoleHandler : AuthorizationHandler<ProjectRoleRequirement>
    {
        private readonly AppDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<ProjectRoleHandler> _logger;

        public ProjectRoleHandler(
            AppDbContext context,
            IHttpContextAccessor contextAccessor,
            ILogger<ProjectRoleHandler> logger)
        {
            _context = context;
            _httpContextAccessor = contextAccessor;
            _logger = logger;
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ProjectRoleRequirement requirement)
        {
            //TryParse: hibás formátumú claim vagy route érték elutasításhoz vezet,
            //nem kezeletlen FormatException-höz (ami 500-at adna a jogosultsági rétegből)
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return;

            var httpContext = _httpContextAccessor.HttpContext;
            var projectIdStr = httpContext?.GetRouteValue("projectId")?.ToString();
            if (projectIdStr == null || !Guid.TryParse(projectIdStr, out var projectId))
                return;

            var member = await _context.ProjectMembers
                .FirstOrDefaultAsync(pm => pm.ProjectId == projectId && pm.UserId == userId);
            if (member == null)
                return;

            //A hierarchia a ProjectRoles konstansokból jön, nem hardcode-olt stringekből
            var userRoleRank = ProjectRoles.RankOf(member.ProjectRole);
            var requiredRoleRank = ProjectRoles.RankOf(requirement.RequiredRole);

            //Ismeretlen szerepkör vagy ismeretlen követelmény: fail-closed.
            //Enélkül két -1 összehasonlítása (-1 >= -1) igaz lenne, és átengedné a kérést.
            if (userRoleRank < 0 || requiredRoleRank < 0)
            {
                _logger.LogWarning(
                    "Ismeretlen szerepkör a jogosultság-ellenőrzésben | UserRole: {UserRole} | RequiredRole: {RequiredRole}",
                    member.ProjectRole, requirement.RequiredRole);
                return;
            }

            if (userRoleRank >= requiredRoleRank)
                context.Succeed(requirement);
        }

    }
}
