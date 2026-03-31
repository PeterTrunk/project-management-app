using ProjectManager.API.DTOs.ProjectTask;

namespace ProjectManager.API.Services.ProjectTaskService
{
    public interface ITaskService
    {
        Task<TaskResponseDto> CreateTaskAsync(Guid createdById, Guid projectId, CreateTaskDto dto);
        Task<TaskResponseDto> GetTaskByIdAsync(Guid taskId, Guid projectId);
        Task<List<TaskResponseDto>> GetTasksAsync(Guid projectId, Guid? boardId = null, Guid? sprintId = null);
        Task<TaskResponseDto> UpdateTaskAsync(Guid taskId, UpdateTaskDto dto);
        Task<TaskResponseDto> MoveTaskAsync(Guid projectId, Guid taskId, MoveTaskDto dto);
        Task DeleteTaskAsync(Guid taskId);
        Task<TaskResponseDto> AssignTaskToBoardAsync(Guid projectId, Guid taskId, AssignTaskToBoardDto dto);
    }
}
