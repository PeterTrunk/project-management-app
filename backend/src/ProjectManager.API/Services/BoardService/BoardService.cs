using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Data;
using ProjectManager.API.DTOs.Boards;
using ProjectManager.API.DTOs.Columns;
using ProjectManager.API.Hubs;
using ProjectManager.API.Model;
using ProjectManager.API.Services.ActivityService;
using ProjectManager.API.Services.CurrentUserService;

namespace ProjectManager.API.Services.BoardService
{
    public class BoardService : IBoardService
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<ProjectHub> _hubContext;
        private readonly ICurrentUserService _currentUserService;
        private readonly IActivityService _activityService;
        private readonly ILogger<BoardService> _logger;

        public BoardService(
            AppDbContext context, 
            IHubContext<ProjectHub> hubContext, 
            ICurrentUserService currentUserService, 
            IActivityService activityService,
            ILogger<BoardService> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _currentUserService = currentUserService;
            _activityService = activityService;
            _logger = logger;
        }
        public async Task<BoardResponseDto> CreateBoardAsync(Guid projectId, CreateBoardDto dto)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található");

            // Ha az új board isDefault = true, akkor a többi boardon hamissá tesszük
            if (dto.IsDefault)
            {
                //Valószínüleg egy lenne, biztonság kedvéért legyen teljes ellenőrzés
                var currentDefaultBoards = await _context.Boards
                    .Where(b => b.ProjectId == projectId && b.IsDefault)
                    .ToListAsync();

                foreach (var defaultBoard in currentDefaultBoards)
                {
                    defaultBoard.IsDefault = false;
                }
            }
            
            var board = new Board
            {
                ProjectId = dto.ProjectId,
                Name = dto.Name,
                Description = dto.Description,
                IsDefault = dto.IsDefault
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

            await _context.SaveChangesAsync();

            try
            {
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("BoardCreated", new
                    {
                        id = board.Id,
                        name = board.Name,
                        description = board.Description,
                        isDefault = board.IsDefault
                    });
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                    "BoardCreated", projectId);
            }

            try
            {
                var activity = await _activityService.LogActivityAsync(
                    projectId,
                    "Board",
                    board.Id,
                    "Created",
                    $"{_currentUserService.DisplayName} létrehozta a {board.Name} boardot"
                );
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("ActivityCreated", activity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: ActivityCreated | ProjectId: {ProjectId}", projectId);
            }

            var response = new BoardResponseDto
            {
                Id = board.Id,
                ProjectId = board.ProjectId,
                Name = board.Name,
                Description = board.Description,
                IsDefault = board.IsDefault,
                CreatedAt = board.CreatedAt,
                UpdatedAt = board.UpdatedAt
            };
            return response;
        }

        public async Task DeleteBoardAsync(Guid projectId, Guid boardId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található");

            var board = await _context.Boards.FirstOrDefaultAsync(b => b.Id == boardId);
            if(board == null)
                throw new Exception("Board nem található");
            
            _context.Boards.Remove(board);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new Exception("A board időközben módosult, kérjük próbáld újra!");
            }
            
            try
            {
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("BoardDeleted", new { boardId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                    "BoardDeleted", projectId);
            }

            try
            {
                var activity = await _activityService.LogActivityAsync(
                    projectId,
                    "Board",
                    board.Id,
                    "Deleted",
                    $"{_currentUserService.DisplayName} törölte a {board.Name} boardot"
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

        public async Task<List<BoardResponseDto>> GetBoardsAsync(
            Guid projectId, 
            string? scope = null)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található");

            var boardsQuery = _context.Boards
                .Where(b => b.ProjectId == projectId)
                .AsQueryable();

            if (scope == "initial")
                boardsQuery = boardsQuery.Include(b => b.ColumnDefinitions.Where(c => !c.IsDeleted));

            var boards = await boardsQuery.ToListAsync();

            return boards.Select(b => new BoardResponseDto
            {
                Id = b.Id,
                ProjectId = b.ProjectId,
                Name = b.Name,
                Description = b.Description,
                IsDefault = b.IsDefault,
                CreatedAt = b.CreatedAt,
                UpdatedAt = b.UpdatedAt,
                Columns = scope == "initial"
                    ? b.ColumnDefinitions
                        .OrderBy(c => c.Position)
                        .Select(c => new ColumnResponseDto
                        {
                            Id = c.Id,
                            BoardId = c.BoardId,
                            Name = c.Name,
                            MapsToStatus = c.MapsToStatus,
                            Position = c.Position,
                            WipLimit = c.WipLimit,
                            RowVersion = c.xmin
                        }).ToList()
                    : null,
                RowVersion = b.xmin
            }).ToList();
        }

        public async Task<BoardResponseDto> UpdateBoardAsync(Guid projectId, Guid boardId, UpdateBoardDto dto)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található");

            var board = await _context.Boards.FirstOrDefaultAsync(b => b.Id == boardId);
            if (board == null)
                throw new Exception("Board nem található");

            _context.Entry(board).OriginalValues["xmin"] = dto.RowVersion;

            if (dto.Name != null) board.Name = dto.Name;
            if(dto.Description != null) board.Description = dto.Description;
            if (dto.IsDefault != null)
            {
                board.IsDefault = dto.IsDefault.Value;
                if (dto.IsDefault.Value)
                {
                    //Valószínüleg egy lenne, biztonság kedvéért legyen teljes ellenőrzés, kivéve az updatelt esetén
                    var currentDefaultBoards = await _context.Boards
                        .Where(b => b.ProjectId == projectId && b.IsDefault && b.Id != boardId)
                        .ToListAsync();

                    foreach (var defaultBoard in currentDefaultBoards)
                    {
                        defaultBoard.IsDefault = false;
                    }
                }
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new Exception("A board időközben módosult, kérjük próbáld újra!");
            }


            try
            {
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("BoardUpdated", new
                    {
                        boardId = board.Id,
                        name = board.Name,
                        description = board.Description,
                        isDefault = board.IsDefault,
                        rowVersion = board.xmin
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                    "BoardUpdated", projectId);
            }

            try
            {
                var activity = await _activityService.LogActivityAsync(
                    projectId,
                    "Board",
                    board.Id,
                    "Updated",
                    $"{_currentUserService.DisplayName} módosította a {board.Name} boardot"
                );
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("ActivityCreated", activity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: ActivityCreated | ProjectId: {ProjectId}", projectId);
            }

            return MapToDto(board);
        }

        private BoardResponseDto MapToDto(Board board)
        {
            return new BoardResponseDto
            {
                Id = board.Id,
                ProjectId = board.ProjectId,
                Name = board.Name,
                Description = board.Description,
                IsDefault = board.IsDefault,
                CreatedAt = board.CreatedAt,
                UpdatedAt = board.UpdatedAt,
                RowVersion = board.xmin
            };
        }
    }
}
