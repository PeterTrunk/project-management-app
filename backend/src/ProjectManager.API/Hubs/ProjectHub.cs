using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Data;
using System.Security.Claims;

namespace ProjectManager.API.Hubs
{
    [Authorize]
    public class ProjectHub : Hub
    {
        private readonly AppDbContext _context;

        public ProjectHub(AppDbContext context)
        {
            _context = context;
        }

        public async Task JoinProject(string projectId)
        {
            if (!Guid.TryParse(projectId, out var pid))
                throw new HubException("Érvénytelen projekt azonosító!");

            var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                throw new HubException("Érvénytelen felhasználói azonosító!");

            var isMember = await _context.ProjectMembers
                .AnyAsync(pm => pm.ProjectId == pid && pm.UserId == userId);

            if (!isMember)
                throw new HubException("Nincs jogosultságod ehhez a projekthez!");

            await Groups.AddToGroupAsync(Context.ConnectionId, $"project-{pid}");
        }

        public async Task LeaveProject(string projectId)
        {
            if (!Guid.TryParse(projectId, out var pid))
                throw new HubException("Érvénytelen projekt azonosító!");

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"project-{pid}");
        }
    }
}
