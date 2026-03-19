using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Data;
using ProjectManager.API.DTOs.Columns;
using ProjectManager.API.Model;

namespace ProjectManager.API.Services.ColumnService
{
    public class ColumnService : IColumnService
    {
        private readonly AppDbContext _context;

        public ColumnService(AppDbContext context)
        {
            _context = context;
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

            var column = await _context.ColumnDefinitions.FirstOrDefaultAsync(c => c.Id == columnId);
            if (column == null)
                throw new Exception("Oszlop nem található");

            var hasTask = await _context.ProjectTasks.AnyAsync(t => t.ColumnId == columnId);
            if (hasTask)
                throw new Exception("Az oszlop nem törölhető, mert taskok találhatóak benne!");

            _context.ColumnDefinitions.Remove(column);
            await _context.SaveChangesAsync();
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
                .Where(c => c.BoardId == boardId)
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

            var columnIds = order.Select(o => o.Id).ToList();
            var columns = await _context.ColumnDefinitions
                .Where(c => columnIds.Contains(c.Id))
                .ToListAsync();

            if (columns.Count != order.Count)
                throw new Exception("Egy vagy több oszlop nem található!");
            
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

            var column = await _context.ColumnDefinitions.FirstOrDefaultAsync(c => c.Id == columnId);
            if (column == null)
                throw new Exception("Oszlop nem található");

            if(dto.Name != null) column.Name = dto.Name;
            if(dto.MapsToStatus != null) column.MapsToStatus = dto.MapsToStatus;
            if(dto.WipLimit != null) column.WipLimit = dto.WipLimit;
            await _context.SaveChangesAsync();

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
