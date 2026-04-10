using ProjectManager.API.DTOs.Comments;

namespace ProjectManager.API.Services.CommentService
{
    public interface ICommentService
    {
        Task<List<CommentResponseDto>> GetCommentsAsync(Guid projectId, Guid taskId);
        Task<CommentResponseDto> CommentOnTaskAsync(Guid projectId, Guid taskId, CreateCommentDto dto);
        Task DeleteCommentFromTaskAsync(Guid projectId, Guid taskId, Guid commentId);
    }
}
