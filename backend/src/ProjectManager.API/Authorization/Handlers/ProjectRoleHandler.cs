using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Authorization.Requirements;
using ProjectManager.API.Data;
using System.Security.Claims;

namespace ProjectManager.API.Authorization.Handlers
{
    public class ProjectRoleHandler : AuthorizationHandler<ProjectRoleRequirement>
    {
        private readonly AppDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ProjectRoleHandler(AppDbContext context, IHttpContextAccessor contextAccessor)
        {
            _context = context;
            _httpContextAccessor = contextAccessor;
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ProjectRoleRequirement requirement)
        {
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) 
                return;
            var userId = Guid.Parse(userIdClaim.Value);

            var httpContext = _httpContextAccessor.HttpContext;
            var projectIdStr = httpContext?.GetRouteValue("projectId")?.ToString();
            if (projectIdStr == null)
                return;
            var projectId = Guid.Parse(projectIdStr);

            var member = await _context.ProjectMembers
                .FirstOrDefaultAsync(pm => pm.ProjectId == projectId && pm.UserId == userId);
            if(member == null) 
                return;

            var roleHierarchy = new List<string>
            {
                "Viewer", "Member", "Admin", "Owner"
            };

            var userRoleIndex = roleHierarchy.IndexOf(member.ProjectRole);
            var requiredRoleIndex = roleHierarchy.IndexOf(requirement.RequiredRole);

            if (userRoleIndex >= requiredRoleIndex)
                context.Succeed(requirement);
        }

    }
}
