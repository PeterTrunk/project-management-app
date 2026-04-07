using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Data;
using ProjectManager.API.DTOs.Boards;
using ProjectManager.API.Hubs;
using ProjectManager.API.Model;

namespace ProjectManager.API.Services.BoardService
{
    public class BoardService : IBoardService
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<ProjectHub> _hubContext;

        public BoardService(AppDbContext context, IHubContext<ProjectHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
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
            var doneColumn = new ColumnDefinition
            {
                BoardId = board.Id,
                Name = "Done",
                MapsToStatus = "Done",
                Position = 99
            };
            _context.ColumnDefinitions.AddRange(backlogColumn, doneColumn, toDoColumn);

            await _context.SaveChangesAsync();
            await _hubContext.Clients
                .Group($"project-{projectId}")
                .SendAsync("BoardCreated", new
                {
                    board.Id,
                    board.Name,
                    board.IsDefault
                });

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
            await _context.SaveChangesAsync();
            await _hubContext.Clients
                .Group($"project-{projectId}")
                .SendAsync("BoardDeleted", new { boardId });
        }

        public async Task<List<BoardResponseDto>> GetBoardsAsync(Guid projectId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található");

            var boards = await _context.Boards
                .Where(b => b.ProjectId == projectId)
                .ToListAsync();

            return boards.Select(b => new BoardResponseDto
            {
                Id = b.Id,
                ProjectId = b.ProjectId,
                Name = b.Name,
                Description = b.Description,
                IsDefault = b.IsDefault,
                CreatedAt = b.CreatedAt,
                UpdatedAt = b.UpdatedAt
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

            if(dto.Name != null) board.Name = dto.Name;
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
            
            await _context.SaveChangesAsync();
            await _hubContext.Clients
                .Group($"project-{projectId}")
                .SendAsync("BoardUpdated", new
                {
                    boardId = board.Id,
                    board.Name,
                    board.IsDefault
                });

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
    }
}
