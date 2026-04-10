using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        public SprintController(ISprintService sprintService)
        {
            _sprintService = sprintService;
        }

        [HttpGet]
        [Authorize(Policy = "ProjectViewer")]
        [ProducesResponseType(typeof(List<SprintResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<SprintResponseDto>>> GetSprintsAsync(Guid projectId)
        {
            try
            {
                var response = await _sprintService.GetSprintsAsync(projectId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        [Authorize(Policy = "ProjectAdmin")]
        [ProducesResponseType(typeof(SprintResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<SprintResponseDto>> CreateSprintAsync(Guid projectId, [FromBody] CreateSprintDto dto)
        {
            try
            {
                var response = await _sprintService.CreateSprintAsync(projectId, dto);
                return Created(string.Empty, response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("{sprintId}")]
        [Authorize(Policy = "ProjectAdmin")]
        [ProducesResponseType(typeof(SprintResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<SprintResponseDto>> UpdateSprintAsync(Guid projectId, Guid sprintId, [FromBody] UpdateSprintDto dto)
        {
            try
            {
                var response = await _sprintService.UpdateSprintAsync(projectId, sprintId, dto);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{sprintId}")]
        [Authorize(Policy = "ProjectAdmin")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> DeleteSprintAsync(Guid projectId, Guid sprintId)
        {
            try
            {
                await _sprintService.DeleteSprintAsync(projectId, sprintId);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("{sprintId}/unfinished")]
        [Authorize(Policy = "ProjectViewer")]
        [ProducesResponseType(typeof(List<TaskResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<TaskResponseDto>>> GetUnfinishedTasksAsync(Guid projectId, Guid sprintId)
        {
            try
            {
                var response = await _sprintService.GetUnfinishedTasksAsync(projectId, sprintId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPatch("{sprintId}/plan")]
        [Authorize(Policy = "ProjectAdmin")]
        [ProducesResponseType(typeof(SprintResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<SprintResponseDto>> PlanSprintAsync(Guid projectId, Guid sprintId)
        {
            try
            {
                var response = await _sprintService.PlanSprintAsync(projectId, sprintId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPatch("{sprintId}/activate")]
        [Authorize(Policy = "ProjectAdmin")]
        [ProducesResponseType(typeof(SprintResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<SprintResponseDto>> ActivateSprintAsync(Guid projectId, Guid sprintId)
        {
            try
            {
                var response = await _sprintService.ActivateSprintAsync(projectId, sprintId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("{sprintId}/complete")]
        [Authorize(Policy = "ProjectAdmin")]
        [ProducesResponseType(typeof(SprintResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<SprintResponseDto>> CompleteSprintAsync(Guid projectId, Guid sprintId, [FromBody] Guid? targetSprintId)
        {
            try
            {
                var response = await _sprintService.CompleteSprintAsync(projectId, sprintId, targetSprintId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("{sprintId}/tasks/{taskId}")]
        [Authorize(Policy = "ProjectMember")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> AssignTaskToSprintAsync(Guid projectId, Guid sprintId, Guid taskId)
        {
            try
            {
                await _sprintService.AssignTaskToSprintAsync(projectId, taskId, sprintId);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{sprintId}/tasks/{taskId}")]
        [Authorize(Policy = "ProjectMember")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> RemoveTaskFromSprintAsync(Guid projectId, Guid sprintId, Guid taskId)
        {
            try
            {
                await _sprintService.AssignTaskToSprintAsync(projectId, taskId, null);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
