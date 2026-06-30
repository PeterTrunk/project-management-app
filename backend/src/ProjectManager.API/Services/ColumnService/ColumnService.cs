using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Data;
using ProjectManager.API.DTOs.Columns;
using ProjectManager.API.Hubs;
using ProjectManager.API.Model;
using ProjectManager.API.Services.ActivityService;
using ProjectManager.API.Services.CurrentUserService;

namespace ProjectManager.API.Services.ColumnService
{
    public class ColumnService : IColumnService
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<ProjectHub> _hubContext;
        private readonly ICurrentUserService _currentUserService;
        private readonly IActivityService _activityService;

        public ColumnService(AppDbContext context, IHubContext<ProjectHub> hubContext, ICurrentUserService currentUserService, IActivityService activityService)
        {
            _context = context;
            _hubContext = hubContext;
            _currentUserService = currentUserService;
            _activityService = activityService;
        }

        public async Task<ColumnResponseDto> CreateColumnAsync(Guid projectId, Guid boardId, CreateColumnDto dto)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található");

            var board = await _context.Boards.FirstOrDefaultAsync(b => b.Id == boardId);
            if (board == null)
                throw new Exception("Board nem található");

            var column = new ColumnDefinition
            {
                BoardId = dto.BoardId,
                Name = dto.Name,
                MapsToStatus = dto.MapsToStatus,
                WipLimit = dto.WipLimit,
                Position = dto.Position,
            };
            _context.ColumnDefinitions.Add(column);
            await _context.SaveChangesAsync();
            await _hubContext.Clients
                .Group($"project-{projectId}")
                .SendAsync("ColumnCreated", new
                {
                    column.Id,
                    column.BoardId,
                    column.Name,
                    column.Position,
                    column.MapsToStatus
                });

            try
            {
                var activity = await _activityService.LogActivityAsync(
                    projectId,
                    "Column",
                    column.Id,
                    "Created",
                    $"{_currentUserService.DisplayName} létrehozta a {column.Name} oszlopot"
                );
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("ActivityCreated", activity);
            }
            catch { }

            var response = new ColumnResponseDto
            {
                Id = column.Id,
                BoardId = column.BoardId,
                Name = column.Name,
                MapsToStatus = column.MapsToStatus,
                WipLimit = column.WipLimit,
                Position = column.Position
            };
            return response;
        }
        
        public async Task DeleteColumnAsync(Guid projectId, Guid boardId, Guid columnId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található");

            var board = await _context.Boards.FirstOrDefaultAsync(b => b.Id == boardId);
            if (board == null)
                throw new Exception("Board nem található");

            var column = await _context.ColumnDefinitions
                .FirstOrDefaultAsync(c => c.Id == columnId && !c.IsDeleted);
            if (column == null)
                throw new Exception("Oszlop nem található");
            if (column.Position == 0)
                throw new Exception("A Backlog oszlop nem törölhető!");

            var hasTask = await _context.ProjectTasks.AnyAsync(t => t.ColumnId == columnId);
            if (hasTask)
                throw new Exception("Az oszlop nem törölhető, mert taskok találhatóak benne!");

            //Soft delete
            column.IsDeleted = true;
            column.DeletedAt = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();
            await _hubContext.Clients
                .Group($"project-{projectId}")
                .SendAsync("ColumnDeleted", new { columnId, boardId });

            try
            {
                var activity = await _activityService.LogActivityAsync(
                    projectId,
                    "Column",
                    column.Id,
                    "Deleted",
                    $"{_currentUserService.DisplayName} törölte a {column.Name} oszlopot"
                );
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("ActivityCreated", activity);
            }
            catch { }
        }

        public async Task<List<ColumnResponseDto>> GetColumnsAsync(Guid projectId, Guid boardId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található");

            var board = await _context.Boards.FirstOrDefaultAsync(b => b.Id == boardId);
            if (board == null)
                throw new Exception("Board nem található");

            var columns = await _context.ColumnDefinitions
                .Where(c => c.BoardId == boardId && !c.IsDeleted)
                .ToListAsync();

            return columns.Select(c => new ColumnResponseDto
            {
                Id = c.Id,
                BoardId = c.BoardId,
                Name = c.Name,
                MapsToStatus = c.MapsToStatus,
                WipLimit = c.WipLimit,
                Position = c.Position
            }).ToList();
        }

        public async Task<List<ColumnResponseDto>> OrderColumnsAsync(Guid projectId, Guid boardId, List<ColumnOrderDto> order)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található");

            var board = await _context.Boards.FirstOrDefaultAsync(b => b.Id == boardId);
            if (board == null)
                throw new Exception("Board nem található");

            var backlogColumn = await _context.ColumnDefinitions
                .FirstOrDefaultAsync(c => c.Position == 0 && c.BoardId == boardId);
            var columnIds = order.Select(o => o.Id).ToList();
            var columns = await _context.ColumnDefinitions
                .Where(c => columnIds.Contains(c.Id) && !c.IsDeleted)
                .ToListAsync();

            if (columns.Count != order.Count)
                throw new Exception("Egy vagy több oszlop nem található!");
            if (backlogColumn != null && order.Any(o => o.Id == backlogColumn.Id && o.Position != 0))
                throw new Exception("A Backlog oszlop pozíciója nem változtatható!");

            // Először -1-re állítjuk
            foreach (var col in columns)
                col.Position = -1;
            await _context.SaveChangesAsync();

            // Majd beállítjuk a tényleges order pozíciókat
            foreach (var item in order)
            {
                var col = columns.First(c => c.Id == item.Id);
                col.Position = item.Position;
            }

            await _context.SaveChangesAsync();
            await _hubContext.Clients
                .Group($"project-{projectId}")
                .SendAsync("ColumnsReordered", new
                {
                    boardId,
                    columns = columns.Select(c => new { c.Id, c.Position })
                });

            return columns.Select(c => new ColumnResponseDto
            {
                Id = c.Id,
                BoardId = c.BoardId,
                Name = c.Name,
                MapsToStatus = c.MapsToStatus,
                WipLimit = c.WipLimit,
                Position = c.Position
            }).ToList();
        }

        public async Task<ColumnResponseDto> UpdateColumnAsync(Guid projectId, Guid boardId, Guid columnId, UpdateColumnDto dto)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található");

            var board = await _context.Boards.FirstOrDefaultAsync(b => b.Id == boardId);
            if (board == null)
                throw new Exception("Board nem található");

            var column = await _context.ColumnDefinitions.FirstOrDefaultAsync(c => c.Id == columnId && !c.IsDeleted);
            if (column == null)
                throw new Exception("Oszlop nem található");

            if(dto.Name != null) column.Name = dto.Name;
            if(dto.MapsToStatus != null) column.MapsToStatus = dto.MapsToStatus;
            if(dto.WipLimit != null) column.WipLimit = dto.WipLimit;

            await _context.SaveChangesAsync();
            await _hubContext.Clients
                .Group($"project-{projectId}")
                .SendAsync("ColumnUpdated", new
                {
                    columnId = column.Id,
                    boardId = column.BoardId,
                    column.Name,
                    column.MapsToStatus,
                    column.WipLimit
                });

            try
            {
                var activity = await _activityService.LogActivityAsync(
                    projectId,
                    "Column",
                    column.Id,
                    "Updated",
                    $"{_currentUserService.DisplayName} módosította a {column.Name} oszlopot"
                );
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("ActivityCreated", activity);
            }
            catch { }

            var response = new ColumnResponseDto
            {
                Id = column.Id,
                BoardId = column.BoardId,
                Name = column.Name,
                MapsToStatus = column.MapsToStatus,
                WipLimit = column.WipLimit,
                Position = column.Position
            };
            return response;
        }
    }
}
