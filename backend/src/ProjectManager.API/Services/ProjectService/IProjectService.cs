using ProjectManager.API.DTOs.Project;

namespace ProjectManager.API.Services.ProjectService
{
    public interface IProjectService
    {
        Task<ProjectResponseDto> CreateProjectAsync(Guid ownerId, CreateProjectDto dto);
        Task<List<ProjectResponseDto>> GetProjectsAsync(Guid userId);
        Task<ProjectResponseDto> GetProjectByIdAsync(Guid projectId);
        Task<ProjectResponseDto> UpdateProjectAsync(Guid projectId, UpdateProjectDto dto);
        Task ArchiveProjectAsync(Guid projectId);
        Task UnarchiveProjectAsync(Guid projectId);
    }
}
