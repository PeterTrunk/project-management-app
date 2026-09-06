using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.DTOs.Labels;
using ProjectManager.API.Filters;
using ProjectManager.API.Services.LabelService;
using System.Security.Claims;

namespace ProjectManager.API.Controllers
{
    [ApiController]
    [ServiceFilter(typeof(ProjectNotArchivedFilter))]
    [Route("api/projects/{projectId}/labels")]
    public class LabelController : ControllerBase
    {
        private readonly ILabelService _labelService;
        private readonly ILogger<LabelController> _logger;

        public LabelController(
            ILabelService labelService,
            ILogger<LabelController> logger)
        {
            _labelService = labelService;
            _logger = logger;
        }

        [HttpGet]
        [Authorize(Policy = PolicyNames.ProjectViewer)]
        [ProducesResponseType(typeof(List<LabelResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<LabelResponseDto>>> GetLabelsAsync(Guid projectId)
        {
            var response = await _labelService.GetLabelsAsync(projectId);
            return Ok(response);
        }

        [HttpPost]
        [Authorize(Policy = PolicyNames.ProjectMember)]
        [ProducesResponseType(typeof(LabelResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<LabelResponseDto>> CreateLabelAsync(Guid projectId, [FromBody] CreateLabelDto dto)
        {
            var response = await _labelService.CreateLabelAsync(projectId, dto);
            return Created(string.Empty, response);
        }

        [HttpDelete("{labelId}")]
        [Authorize(Policy = PolicyNames.ProjectMember)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> DeleteLabelAsync(Guid projectId, Guid labelId)
        {
            await _labelService.DeleteLabelAsync(projectId, labelId);
            return NoContent();
        }

        [HttpPost("tasks/{taskId}/labels/{labelId}")]
        [Authorize(Policy = PolicyNames.ProjectMember)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> AddLabelToTask(Guid projectId, Guid taskId, Guid labelId)
        {
            await _labelService.AddLabelToTaskAsync(projectId, taskId, labelId);
            return Ok();
        }

        [HttpDelete("tasks/{taskId}/labels/{labelId}")]
        [Authorize(Policy = PolicyNames.ProjectMember)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> RemoveLabelFromTask(Guid projectId, Guid taskId, Guid labelId)
        {
            await _labelService.RemoveLabelFromTaskAsync(projectId, taskId, labelId);
            return NoContent();
        }
    }
}
