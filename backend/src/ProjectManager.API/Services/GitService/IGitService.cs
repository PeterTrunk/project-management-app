using ProjectManager.API.DTOs.Git;

namespace ProjectManager.API.Services.GitService
{
    public interface IGitService
    {
        Task<List<CommitLinkResponseDto>> GetUnmatchedCommitsAsync(Guid projectId);
        Task<List<PrLinkResponseDto>> GetUnmatchedPrsAsync(Guid projectId);
        Task AssignCommitToTaskAsync(Guid projectId, Guid commitId, Guid taskId);
        Task AssignPrToTaskAsync(Guid projectId, Guid prId, Guid taskId);
    }
}
