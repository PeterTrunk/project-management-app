using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.DTOs.ProjectTask;
using ProjectManager.API.DTOs.Sprints;
using ProjectManager.API.Filters;
using ProjectManager.API.Services.SprintService;


namespace ProjectManager.API.Controllers
{
    [ApiController]
    [ServiceFilter(typeof(ProjectNotArchivedFilter))]
    [Route("api/projects/{projectId}/sprints")]
    public class SprintController : ControllerBase
    {
        private readonly ISprintService _sprintService;
        private readonly ILogger<SprintController> _logger;

        public SprintController(
            ISprintService sprintService,
            ILogger<SprintController> logger)
        {
            _sprintService = sprintService;
            _logger = logger;
        }

        [HttpGet]
        [Authorize(Policy = PolicyNames.ProjectViewer)]
        [ProducesResponseType(typeof(List<SprintResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<SprintResponseDto>>> GetSprintsAsync(
            Guid projectId,
            [FromQuery] string? scope = null)
        {
            var response = await _sprintService.GetSprintsAsync(projectId, scope);
            return Ok(response);
        }

        [HttpPost]
        [Authorize(Policy = PolicyNames.ProjectAdmin)]
        [ProducesResponseType(typeof(SprintResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<SprintResponseDto>> CreateSprintAsync(Guid projectId, [FromBody] CreateSprintDto dto)
        {
            var response = await _sprintService.CreateSprintAsync(projectId, dto);
            return Created(string.Empty, response);
        }

        [HttpPut("{sprintId}")]
        [Authorize(Policy = PolicyNames.ProjectAdmin)]
        [ProducesResponseType(typeof(SprintResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<SprintResponseDto>> UpdateSprintAsync(Guid projectId, Guid sprintId, [FromBody] UpdateSprintDto dto)
        {
            var response = await _sprintService.UpdateSprintAsync(projectId, sprintId, dto);
            return Ok(response);
        }

        [HttpDelete("{sprintId}")]
        [Authorize(Policy = PolicyNames.ProjectAdmin)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> DeleteSprintAsync(Guid projectId, Guid sprintId)
        {
            await _sprintService.DeleteSprintAsync(projectId, sprintId);
            return NoContent();
        }

        [HttpGet("{sprintId}/unfinished")]
        [Authorize(Policy = PolicyNames.ProjectViewer)]
        [ProducesResponseType(typeof(List<TaskResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<TaskResponseDto>>> GetUnfinishedTasksAsync(Guid projectId, Guid sprintId)
        {
            var response = await _sprintService.GetUnfinishedTasksAsync(projectId, sprintId);
            return Ok(response);
        }

        [HttpPatch("{sprintId}/plan")]
        [Authorize(Policy = PolicyNames.ProjectAdmin)]
        [ProducesResponseType(typeof(SprintResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<SprintResponseDto>> PlanSprintAsync(Guid projectId, Guid sprintId)
        {
            var response = await _sprintService.PlanSprintAsync(projectId, sprintId);
            return Ok(response);
        }

        [HttpPatch("{sprintId}/activate")]
        [Authorize(Policy = PolicyNames.ProjectAdmin)]
        [ProducesResponseType(typeof(SprintResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<SprintResponseDto>> ActivateSprintAsync(Guid projectId, Guid sprintId)
        {
            var response = await _sprintService.ActivateSprintAsync(projectId, sprintId);
            return Ok(response);
        }

        [HttpPost("{sprintId}/complete")]
        [Authorize(Policy = PolicyNames.ProjectAdmin)]
        [ProducesResponseType(typeof(SprintResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<SprintResponseDto>> CompleteSprintAsync(Guid projectId, Guid sprintId, [FromBody] Guid? targetSprintId)
        {
            var response = await _sprintService.CompleteSprintAsync(projectId, sprintId, targetSprintId);
            return Ok(response);
        }

        [HttpPost("{sprintId}/tasks/{taskId}")]
        [Authorize(Policy = PolicyNames.ProjectMember)]
        [ProducesResponseType(typeof(TaskResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<TaskResponseDto>> AssignTaskToSprintAsync(
            Guid projectId,
            Guid sprintId,
            Guid taskId,
            [FromBody] AssignTaskToSprintDto dto)
        {
            var response = await _sprintService.AssignTaskToSprintAsync(projectId, taskId, sprintId, dto);
            return Ok(response);
        }

        [HttpDelete("{sprintId}/tasks/{taskId}")]
        [Authorize(Policy = PolicyNames.ProjectMember)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> RemoveTaskFromSprintAsync(Guid projectId, Guid sprintId, Guid taskId, [FromBody] AssignTaskToSprintDto dto)
        {
            await _sprintService.RemoveTaskFromSprintAsync(projectId, taskId, dto);
            return NoContent();
        }
    }
}
