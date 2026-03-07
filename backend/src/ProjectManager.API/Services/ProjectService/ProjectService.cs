using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Data;
using ProjectManager.API.DTOs.Project;
using ProjectManager.API.Model;

namespace ProjectManager.API.Services.ProjectService
{
    public class ProjectService : IProjectService
    {
        private readonly AppDbContext _context;

        public ProjectService(AppDbContext context)
        {
            _context = context;
        }
        public async Task ArchiveProjectAsync(Guid projectId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található");
            project.IsArchived = true;
            await _context.SaveChangesAsync();
        }

        public async Task UnarchiveProjectAsync(Guid projectId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található!");
            project.IsArchived = false;
            await _context.SaveChangesAsync();
        }

        public async Task<ProjectResponseDto> CreateProjectAsync(Guid ownerId, CreateProjectDto dto)
        {
            if (await _context.Projects.AnyAsync(p => p.ProjKey == dto.ProjKey))
                throw new Exception("Projekt Key már létezik!");

            User? owner = await _context.Users.FirstOrDefaultAsync(u => u.Id == ownerId);
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
                MapsToStatus = "backlog",
                Position = 0
            };
            var doneColumn = new ColumnDefinition
            {
                BoardId = board.Id,
                Name = "Done",
                MapsToStatus = "done",
                Position = 99
            };
            _context.ColumnDefinitions.AddRange(backlogColumn, doneColumn);

            await _context.SaveChangesAsync();
            
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

        public async Task<List<ProjectResponseDto>> GetProjectsAsync(Guid userId)
        {
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
                throw new Exception("Tulajdonos nem található!");

            await _context.SaveChangesAsync();

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
