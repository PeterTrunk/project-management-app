using ProjectManager.API.DTOs.Boards;

namespace ProjectManager.API.Services.BoardService
{
    public interface IBoardService
    {
        Task<List<BoardResponseDto>> GetBoardsAsync(Guid projectId, string? scope = null);
        Task<BoardResponseDto> CreateBoardAsync(Guid projectId, CreateBoardDto dto);
        Task<BoardResponseDto> UpdateBoardAsync(Guid projectId, Guid boardId, UpdateBoardDto dto);
        Task DeleteBoardAsync(Guid projectId, Guid boardId);
    }
}
