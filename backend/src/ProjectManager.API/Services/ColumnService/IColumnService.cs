using ProjectManager.API.DTOs.Columns;

namespace ProjectManager.API.Services.ColumnService
{
    public interface IColumnService
    {
        Task<List<ColumnResponseDto>> GetColumnsAsync(Guid projectId, Guid boardId);
        Task<ColumnResponseDto> CreateColumnAsync(Guid projectId, Guid boardId, CreateColumnDto dto);
        Task<ColumnResponseDto> UpdateColumnAsync(Guid projectId, Guid boardId, Guid columnId, UpdateColumnDto dto);
        Task DeleteColumnAsync(Guid projectId, Guid boardId, Guid columnId);
        Task<List<ColumnResponseDto>> OrderColumnsAsync(Guid projectId, Guid boardId, List<ColumnOrderDto> order);
    }
}
