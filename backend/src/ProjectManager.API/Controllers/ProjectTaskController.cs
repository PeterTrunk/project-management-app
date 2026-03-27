using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManager.API.DTOs.ProjectTask;
using ProjectManager.API.Services.ProjectTaskService;
using System.Security.Claims;

namespace ProjectManager.API.Controllers
{
    [ApiController]
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
                var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var response = await _taskService.CreateTaskAsync(userId, projectId, dto);
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
        public async Task<ActionResult> DeleteTaskAsync(Guid taskId)
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
        public async Task<ActionResult<TaskResponseDto>> GetTaskByIdAsync(Guid taskId, Guid projectId)
        {
            try
            {
                var response = await _taskService.GetTaskByIdAsync(taskId, projectId);
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
        public async Task<ActionResult<List<TaskResponseDto>>> GetTasksAsync(Guid projectId, [FromQuery] Guid? boardId, [FromQuery] Guid? sprintId)
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
        public async Task<ActionResult<TaskResponseDto>> UpdateTaskAsync(Guid taskId, [FromBody] UpdateTaskDto dto)
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

    }
}
