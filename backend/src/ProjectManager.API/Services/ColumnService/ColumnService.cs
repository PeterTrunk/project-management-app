using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Data;
using ProjectManager.API.DTOs.Columns;
using ProjectManager.API.Hubs;
using ProjectManager.API.Model;
using ProjectManager.API.Services.ActivityService;
using ProjectManager.API.Services.CurrentUserService;
using System.Data;

namespace ProjectManager.API.Services.ColumnService
{
    public class ColumnService : IColumnService
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<ProjectHub> _hubContext;
        private readonly ICurrentUserService _currentUserService;
        private readonly IActivityService _activityService;
        private readonly ILogger<ColumnService> _logger;

        public ColumnService(
            AppDbContext context, 
            IHubContext<ProjectHub> hubContext, 
            ICurrentUserService currentUserService, 
            IActivityService activityService,
            ILogger<ColumnService> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _currentUserService = currentUserService;
            _activityService = activityService;
            _logger = logger;
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

            try
            {
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("ColumnCreated", new
                    {
                        id = column.Id,
                        boardId = column.BoardId,
                        name = column.Name,
                        position = column.Position,
                        mapsToStatus = column.MapsToStatus,
                        wipLimit = column.WipLimit,
                        rowVersion = column.xmin
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                    "ColumnCreated", projectId);
            }

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
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: ActivityCreated | ProjectId: {ProjectId}", projectId);
            }

            return MapToDto(column);
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

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new Exception("Az oszlop időközben módosult, kérjük próbáld újra!");
            }

            try
            {
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("ColumnDeleted", new { columnId, boardId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                    "ColumnDeleted", projectId);
            }

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
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: ActivityCreated | ProjectId: {ProjectId}", projectId);
            }
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

            return columns.Select(MapToDto).ToList();
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

            using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                // RowVersion ellenőrzés minden oszlopra
                foreach (var item in order)
                {
                    var col = columns.First(c => c.Id == item.Id);
                    _context.Entry(col).OriginalValues["xmin"] = item.RowVersion;
                }

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

                // Új utolsó oszlop meghatározása (legnagyobb position, kivéve 0)
                var newLastColumnId = order
                    .Where(o => o.Position > 0)
                    .OrderByDescending(o => o.Position)
                    .First().Id;

                // Többi oszlop ID-jai
                var nonLastColumnIds = order
                    .Where(o => o.Id != newLastColumnId && o.Position > 0)
                    .Select(o => o.Id)
                    .ToList();

                // Új utolsó oszlop taskjain completedAt beállítása
                var tasksInNewLastColumn = await _context.ProjectTasks
                    .Where(t => t.ColumnId == newLastColumnId && t.CompletedAt == null)
                    .ToListAsync();
                foreach (var task in tasksInNewLastColumn)
                    task.CompletedAt = DateTime.UtcNow;

                // Többi oszlop taskjain completedAt törlése
                var tasksInOtherColumns = await _context.ProjectTasks
                    .Where(t => nonLastColumnIds.Contains((Guid)t.ColumnId!) && t.CompletedAt != null)
                    .ToListAsync();
                foreach (var task in tasksInOtherColumns)
                    task.CompletedAt = null;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var updatedColumns = await _context.ColumnDefinitions
                    .Where(c => columnIds.Contains(c.Id) && !c.IsDeleted)
                    .ToListAsync();

                // ColumnsReordered broadcast
                try
                {
                    await _hubContext.Clients
                        .Group($"project-{projectId}")
                        .SendAsync("ColumnsReordered", new
                        {
                            boardId,
                            columns = updatedColumns.Select(c => new {
                                id = c.Id,
                                position = c.Position,
                                rowVersion = c.xmin
                            })
                        });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                        "ColumnsReordered", projectId);
                }

                // TaskMoved broadcast az érintett taskokra
                var allAffectedTasks = tasksInNewLastColumn.Concat(tasksInOtherColumns).ToList();
                foreach (var task in allAffectedTasks)
                {
                    try
                    {
                        await _hubContext.Clients
                            .Group($"project-{projectId}")
                            .SendAsync("TaskMoved", new
                            {
                                taskId = task.Id,
                                boardId = task.BoardId,
                                columnId = task.ColumnId,
                                sprintId = task.SprintId,
                                position = task.Position,
                                completedAt = task.CompletedAt,
                                rowVersion = task.xmin
                            });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                            "TaskMoved", projectId);
                    }
                }

                return updatedColumns.Select(MapToDto).ToList();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
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

            _context.Entry(column).OriginalValues["xmin"] = dto.RowVersion;

            if (dto.Name != null) column.Name = dto.Name;
            if(dto.MapsToStatus != null) column.MapsToStatus = dto.MapsToStatus;
            column.WipLimit = dto.WipLimit; //mindig frissítjük, az != null szabály ez esetben nem tartható mert NULL egy valid érték lehet.

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new Exception("Az oszlop időközben módosult, kérjük próbáld újra!");
            }

            try
            {
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("ColumnUpdated", new
                    {
                        columnId = column.Id,
                        boardId = column.BoardId,
                        name = column.Name,
                        mapsToStatus = column.MapsToStatus,
                        wipLimit = column.WipLimit,
                        rowVersion = column.xmin
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                    "ColumnUpdated", projectId);
            }

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
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: ActivityCreated | ProjectId: {ProjectId}", projectId);
            }

            return MapToDto(column);
        }

        private ColumnResponseDto MapToDto(ColumnDefinition column)
        {
            return new ColumnResponseDto
            {
                Id = column.Id,
                BoardId = column.BoardId,
                Name = column.Name,
                MapsToStatus = column.MapsToStatus,
                WipLimit = column.WipLimit,
                Position = column.Position,
                RowVersion = column.xmin
            };
        }
    }
}
