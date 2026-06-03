using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Data;
using ProjectManager.API.DTOs.Project;
using ProjectManager.API.Hubs;
using ProjectManager.API.Model;
using ProjectManager.API.Services.ActivityService;
using ProjectManager.API.Services.CurrentUserService;

namespace ProjectManager.API.Services.ProjectService
{
    public class ProjectService : IProjectService
    {
        private readonly AppDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly IHubContext<ProjectHub> _hubContext;
        private readonly IActivityService _activityService;

        public ProjectService(AppDbContext context, ICurrentUserService currentUserService, IHubContext<ProjectHub> hubContext, IActivityService activityService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _hubContext = hubContext;
            _activityService = activityService;
        }

        public async Task DeleteProjectAsync(Guid projectId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található!");
            _context.Projects.Remove(project);
            await _context.SaveChangesAsync();
        }

        public async Task ArchiveProjectAsync(Guid projectId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található");
            project.IsArchived = true;
            await _context.SaveChangesAsync();
            await _hubContext.Clients
                .Group($"project-{projectId}")
                .SendAsync("ProjectArchived", new { projectId });

            try
            {
                var activity = await _activityService.LogActivityAsync(
                    projectId,
                    "Project",
                    projectId,
                    "Archived",
                    $"{_currentUserService.DisplayName} archivált a projektet"
                );
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("ActivityCreated", activity);
            }
            catch { }
        }

        public async Task UnarchiveProjectAsync(Guid projectId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található!");
            project.IsArchived = false;
            await _context.SaveChangesAsync();
            await _hubContext.Clients
                .Group($"project-{projectId}")
                .SendAsync("ProjectUnarchived", new { projectId });

            try
            {
                var activity = await _activityService.LogActivityAsync(
                    projectId,
                    "Project",
                    projectId,
                    "Unarchived",
                    $"{_currentUserService.DisplayName} dearchivált a projektet"
                );
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("ActivityCreated", activity);
            }
            catch { }
        }

        public async Task<ProjectResponseDto> CreateProjectAsync(CreateProjectDto dto)
        {
            var ownerId = _currentUserService.UserId;
            var owner = await _context.Users.FirstOrDefaultAsync(u => u.Id == ownerId);
            if (owner == null)
                throw new Exception("Felhasználó nem található!");
            
            var project = new Project
            {
                Name = dto.Name,
                ProjKey = dto.ProjKey,
                Description = dto.Description,
                IsArchived = false,
                OwnerId = ownerId
            };
            _context.Projects.Add(project);

            var projectCounter = new ProjectCounter
            {
                ProjectId = project.Id,
                LastNum = 0
            };
            _context.ProjectCounters.Add(projectCounter);

            var projectMember = new ProjectMember
            {
                ProjectId = project.Id,
                UserId = owner.Id,
                ProjectRole = "Owner",
                JoinedAt = DateTime.UtcNow
            };
            _context.ProjectMembers.Add(projectMember);

            var board = new Board
            {
                ProjectId = project.Id,
                Name = "Main Board",
                IsDefault = true
            };
            _context.Boards.Add(board);

            var backlogColumn = new ColumnDefinition
            {
                BoardId = board.Id,
                Name = "Backlog",
                MapsToStatus = "Backlog",
                Position = 0
            };
            var toDoColumn = new ColumnDefinition
            {
                BoardId = board.Id,
                Name = "To Do",
                MapsToStatus = "To Do",
                Position = 1
            };
            var inProgressColumn = new ColumnDefinition
            {
                BoardId = board.Id,
                Name = "In Progress",
                MapsToStatus = "In Progress",
                Position = 2
            };
            var doneColumn = new ColumnDefinition
            {
                BoardId = board.Id,
                Name = "Done",
                MapsToStatus = "Done",
                Position = 99
            };
            _context.ColumnDefinitions.AddRange(backlogColumn, doneColumn, toDoColumn, inProgressColumn);

            var initSprint = new Sprint
            {
                ProjectId = project.Id,
                Name = "Sprint 0",
                Goal = "Created with project init.",
                StartDate = DateTime.UtcNow,
                State = "Active"
            };
            _context.Sprints.Add(initSprint);

            await _context.SaveChangesAsync();

            try
            {
                var activity = await _activityService.LogActivityAsync(
                    project.Id,
                    "Project",
                    project.Id,
                    "Created",
                    $"{_currentUserService.DisplayName} létrehozta a {project.Name} projektet"
                );
                await _hubContext.Clients
                    .Group($"project-{project.Id}")
                    .SendAsync("ActivityCreated", activity);
            }
            catch { }

            var response = new ProjectResponseDto
            {
                Id = project.Id,
                Name = project.Name,
                ProjKey = project.ProjKey,
                Description = project.Description,
                OwnerName = owner.DisplayName,
                IsArchived = project.IsArchived,
                CreatedAt = project.CreatedAt,
                UpdatedAt = project.UpdatedAt
            };
            return response;
        }
        
        public async Task<ProjectResponseDto> GetProjectByIdAsync(Guid projectId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található");

            var owner = await _context.Users.FirstOrDefaultAsync(u => u.Id == project.OwnerId);
            if (owner == null)
                throw new Exception("Felhasználó nem található!");
            
            var response = new ProjectResponseDto
            {
                Id = project.Id,
                Name = project.Name,
                ProjKey = project.ProjKey,
                Description = project.Description,
                OwnerName = owner.DisplayName,
                IsArchived = project.IsArchived,
                CreatedAt = project.CreatedAt,
                UpdatedAt = project.UpdatedAt
            };
            return response;
        }

        public async Task<List<ProjectResponseDto>> GetProjectsAsync()
        {
            var userId = _currentUserService.UserId;

            var projects = await _context.Projects
                .Where(p => _context.ProjectMembers
                    .Any(pm => pm.ProjectId == p.Id && pm.UserId == userId))
                .Include(p => p.Owner)
                .ToListAsync(); 

            return projects.Select(p => new ProjectResponseDto 
            {
                Id = p.Id,
                Name = p.Name,
                ProjKey= p.ProjKey,
                Description = p.Description,
                OwnerName = p.Owner.DisplayName,
                IsArchived = p.IsArchived,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            }).ToList();
        }

        public async Task<ProjectResponseDto> UpdateProjectAsync(Guid projectId, UpdateProjectDto dto)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
                throw new Exception("Projekt nem található");

            if (dto.Name != null) project.Name = dto.Name;
            if (dto.Description != null) project.Description = dto.Description;
            if (dto.IsArchived.HasValue) project.IsArchived = dto.IsArchived.Value;

            var owner = await _context.Users.FirstOrDefaultAsync(u => u.Id == project.OwnerId);
            if (owner == null)
                throw new Exception("Tulajdonos nem található");

            await _context.SaveChangesAsync();
            await _hubContext.Clients
                .Group($"project-{projectId}")
                .SendAsync("ProjectUpdated", new
                {
                    projectId,
                    project.Name,
                    project.Description
                });

            try
            {
                var activity = await _activityService.LogActivityAsync(
                    projectId,
                    "Project",
                    projectId,
                    "Updated",
                    $"{_currentUserService.DisplayName} módosította a projektet"
                );
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("ActivityCreated", activity);
            }
            catch { }

            var response = new ProjectResponseDto
            {
                Id = project.Id,
                Name = project.Name,
                ProjKey = project.ProjKey,
                Description = project.Description,
                OwnerName = owner.DisplayName,
                IsArchived = project.IsArchived,
                CreatedAt = project.CreatedAt,
                UpdatedAt = project.UpdatedAt


            };
            return response;
        }
    }
}
