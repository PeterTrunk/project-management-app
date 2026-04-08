using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManager.API.DTOs.Labels;
using ProjectManager.API.Services.LabelService;
using System.Security.Claims;

namespace ProjectManager.API.Controllers
{
    [ApiController]
    [Route("api/projects/{projectId}/labels")]
    public class LabelController : ControllerBase
    {
        private readonly ILabelService _labelService;

        public LabelController(ILabelService labelService)
        {
            _labelService = labelService;
        }

        [HttpGet]
        [Authorize(Policy = "ProjectViewer")]
        [ProducesResponseType(typeof(List<LabelResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<LabelResponseDto>>> GetLabelsAsync(Guid projectId)
        {
            try
            {
                var response = await _labelService.GetLabelsAsync(projectId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        [Authorize(Policy = "ProjectMember")]
        [ProducesResponseType(typeof(LabelResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<LabelResponseDto>> CreateLabelAsync(Guid projectId, [FromBody] CreateLabelDto dto)
        {
            try
            {
                var response = await _labelService.CreateLabelAsync(projectId, dto);
                return Created(string.Empty, response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{labelId}")]
        [Authorize(Policy = "ProjectMember")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> DeleteLabelAsync(Guid projectId, Guid labelId)
        {
            try
            {
                await _labelService.DeleteLabelAsync(projectId, labelId);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("tasks/{taskId}/labels/{labelId}")]
        [Authorize(Policy = "ProjectMember")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> AddLabelToTask(Guid projectId, Guid taskId, Guid labelId)
        {
            try
            {
                await _labelService.AddLabelToTaskAsync(projectId, taskId, labelId);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("tasks/{taskId}/labels/{labelId}")]
        [Authorize(Policy = "ProjectMember")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> RemoveLabelFromTask(Guid projectId, Guid taskId, Guid labelId)
        {
            try
            {
                await _labelService.RemoveLabelFromTaskAsync(projectId, taskId, labelId);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
