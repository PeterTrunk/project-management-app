using ProjectManager.API.DTOs.ProjectTask;
using ProjectManager.API.DTOs.Sprints;

namespace ProjectManager.API.Services.SprintService
{
    public interface ISprintService
    {
        Task<List<SprintResponseDto>> GetSprintsAsync(Guid projectId, string? scope = null);
        Task<SprintResponseDto> CreateSprintAsync(Guid projectId, CreateSprintDto dto);
        Task<SprintResponseDto> UpdateSprintAsync(Guid projectId, Guid sprintId, UpdateSprintDto dto);
        Task DeleteSprintAsync(Guid projectId, Guid sprintId);
        Task<SprintResponseDto> ActivateSprintAsync(Guid projectId, Guid sprintId);
        Task<SprintResponseDto> CompleteSprintAsync(Guid projectId, Guid sprintId, Guid? targetSprintId);
        Task<SprintResponseDto> PlanSprintAsync(Guid projectId, Guid sprintId);
        Task<List<TaskResponseDto>> GetUnfinishedTasksAsync(Guid projectId, Guid sprintId);
        Task AssignTaskToSprintAsync(Guid projectId, Guid taskId, Guid? sprintId);
    }
}
