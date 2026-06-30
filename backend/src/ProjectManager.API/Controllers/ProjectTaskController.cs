using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        public ProjectTaskController(ITaskService taskService)
        {
            _taskService = taskService;
        }

        [HttpPost]
        [Authorize(Policy = "ProjectMember")]
        [ProducesResponseType(typeof(TaskResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<TaskResponseDto>> CreateTaskAsync(Guid projectId, [FromBody] CreateTaskDto dto)
        {
            try
            {
                var response = await _taskService.CreateTaskAsync(projectId, dto);
                return Created(string.Empty, response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{taskId}")]
        [Authorize(Policy = "ProjectAdmin")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> DeleteTaskAsync(Guid projectId, Guid taskId)
        {
            try
            {
                await _taskService.DeleteTaskAsync(taskId);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("{taskId}")]
        [Authorize(Policy = "ProjectViewer")]
        [ProducesResponseType(typeof(TaskResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<TaskResponseDto>> GetTaskByIdAsync(Guid projectId, Guid taskId)
        {
            try
            {
                var response = await _taskService.GetTaskByIdAsync(projectId, taskId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        [Authorize(Policy = "ProjectViewer")]
        [ProducesResponseType(typeof(List<TaskResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<TaskResponseDto>>> GetTasksAsync(
            Guid projectId, 
            [FromQuery] Guid? boardId, 
            [FromQuery] Guid? sprintId,
            [FromQuery] string? scope = null)
        {
            try
            {
                var response = await _taskService.GetTasksAsync(projectId, boardId, sprintId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPatch("{taskId}/move")]
        [Authorize(Policy = "ProjectMember")]
        [ProducesResponseType(typeof(TaskResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<TaskResponseDto>> MoveTaskAsync(Guid projectId, Guid taskId, [FromBody] MoveTaskDto dto)
        {
            try
            {
                var response = await _taskService.MoveTaskAsync(projectId, taskId, dto);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPatch("{taskId}")]
        [Authorize(Policy = "ProjectMember")]
        [ProducesResponseType(typeof(TaskResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<TaskResponseDto>> UpdateTaskAsync(Guid projectId, Guid taskId, [FromBody] UpdateTaskDto dto)
        {
            try
            {
                var response = await _taskService.UpdateTaskAsync(taskId, dto);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("{taskId}/board")]
        [Authorize(Policy = "ProjectMember")]
        [ProducesResponseType(typeof(TaskResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<TaskResponseDto>> AssignTaskToBoardAsync(Guid projectId, Guid taskId, [FromBody] AssignTaskToBoardDto dto)
        {
            try
            {
                var response = await _taskService.AssignTaskToBoardAsync(projectId, taskId, dto);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("{taskId}/assignees/{userId}")]
        [Authorize(Policy = "ProjectMember")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> AddAssigneeAsync(Guid projectId, Guid taskId, Guid userId)
        {
            try
            {
                await _taskService.AddAssigneeAsync(projectId, taskId, userId);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{taskId}/assignees/{userId}")]
        [Authorize(Policy = "ProjectMember")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> RemoveAssigneeAsync(Guid projectId, Guid taskId, Guid userId)
        {
            try
            {
                await _taskService.RemoveAssigneeAsync(projectId, taskId, userId);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
