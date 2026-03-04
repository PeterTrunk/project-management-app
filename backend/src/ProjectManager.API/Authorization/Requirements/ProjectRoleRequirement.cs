using Microsoft.AspNetCore.Authorization;

namespace ProjectManager.API.Authorization.Requirements
{
    public class ProjectRoleRequirement : IAuthorizationRequirement
    {
        public string RequiredRole { get; }

        public ProjectRoleRequirement(string requiredRole) 
        {
            RequiredRole = requiredRole;
        }
    }
}
