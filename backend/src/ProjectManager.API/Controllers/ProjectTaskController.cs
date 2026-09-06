using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.DTOs.ProjectTask;
using ProjectManager.API.Filters;
using ProjectManager.API.Services.ProjectTaskService;

namespace ProjectManager.API.Controllers
{
    [ApiController]
    [ServiceFilter(typeof(ProjectNotArchivedFilter))]
    [Route("api/projects/{projectId}/tasks")]
    public class ProjectTaskController : ControllerBase
    {
        private readonly ITaskService _taskService;
        private readonly ILogger<ProjectTaskController> _logger;

        public ProjectTaskController(
            ITaskService taskService,
            ILogger<ProjectTaskController> logger)
        {
            _taskService = taskService;
            _logger = logger;
        }

        [HttpPost]
        [Authorize(Policy = PolicyNames.ProjectMember)]
        [ProducesResponseType(typeof(TaskResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<TaskResponseDto>> CreateTaskAsync(Guid projectId, [FromBody] CreateTaskDto dto)
        {
            var response = await _taskService.CreateTaskAsync(projectId, dto);
            return Created(string.Empty, response);
        }

        [HttpDelete("{taskId}")]
        [Authorize(Policy = PolicyNames.ProjectAdmin)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> DeleteTaskAsync(Guid projectId, Guid taskId)
        {
            await _taskService.DeleteTaskAsync(projectId, taskId);
            return NoContent();
        }

        [HttpGet("{taskId}")]
        [Authorize(Policy = PolicyNames.ProjectViewer)]
        [ProducesResponseType(typeof(TaskResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<TaskResponseDto>> GetTaskByIdAsync(Guid projectId, Guid taskId)
        {
            var response = await _taskService.GetTaskByIdAsync(projectId, taskId);
            return Ok(response);
        }

        [HttpGet]
        [Authorize(Policy = PolicyNames.ProjectViewer)]
        [ProducesResponseType(typeof(List<TaskResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<TaskResponseDto>>> GetTasksAsync(
            Guid projectId, 
            [FromQuery] Guid? boardId, 
            [FromQuery] Guid? sprintId,
            [FromQuery] string? scope = null)
        {
            var response = await _taskService.GetTasksAsync(projectId, boardId, sprintId, scope);
            return Ok(response);
        }

        [HttpPatch("{taskId}/move")]
        [Authorize(Policy = PolicyNames.ProjectMember)]
        [ProducesResponseType(typeof(TaskResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<TaskResponseDto>> MoveTaskAsync(Guid projectId, Guid taskId, [FromBody] MoveTaskDto dto)
        {
            var response = await _taskService.MoveTaskAsync(projectId, taskId, dto);
            return Ok(response);
        }

        [HttpPatch("{taskId}")]
        [Authorize(Policy = PolicyNames.ProjectMember)]
        [ProducesResponseType(typeof(TaskResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<TaskResponseDto>> UpdateTaskAsync(Guid projectId, Guid taskId, [FromBody] UpdateTaskDto dto)
        {
            var response = await _taskService.UpdateTaskAsync(projectId, taskId, dto);
            return Ok(response);
        }

        [HttpPost("{taskId}/board")]
        [Authorize(Policy = PolicyNames.ProjectMember)]
        [ProducesResponseType(typeof(TaskResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<TaskResponseDto>> AssignTaskToBoardAsync(Guid projectId, Guid taskId, [FromBody] AssignTaskToBoardDto dto)
        {
            var response = await _taskService.AssignTaskToBoardAsync(projectId, taskId, dto);
            return Ok(response);
        }

        [HttpPost("{taskId}/assignees/{userId}")]
        [Authorize(Policy = PolicyNames.ProjectMember)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> AddAssigneeAsync(Guid projectId, Guid taskId, Guid userId)
        {
            await _taskService.AddAssigneeAsync(projectId, taskId, userId);
            return Ok();
        }

        [HttpDelete("{taskId}/assignees/{userId}")]
        [Authorize(Policy = PolicyNames.ProjectMember)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> RemoveAssigneeAsync(Guid projectId, Guid taskId, Guid userId)
        {
            await _taskService.RemoveAssigneeAsync(projectId, taskId, userId);
            return NoContent();
        }
    }
}
