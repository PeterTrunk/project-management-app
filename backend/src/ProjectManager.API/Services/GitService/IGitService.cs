using ProjectManager.API.DTOs.Git;

namespace ProjectManager.API.Services.GitService
{
    public interface IGitService
    {
        Task<List<LinkedCommitResponseDto>> GetCommitLinksAsync(Guid projectId);
        Task<List<LinkedPrResponseDto>> GetPrLinksAsync(Guid projectId);
        Task AssignCommitToTaskAsync(Guid projectId, Guid commitId, Guid taskId);
        Task AssignPrToTaskAsync(Guid projectId, Guid prId, Guid taskId);
    }
}
