using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.DTOs.Columns;
using ProjectManager.API.Filters;
using ProjectManager.API.Model;
using ProjectManager.API.Services.ColumnService;

namespace ProjectManager.API.Controllers
{
    [ApiController]
    [ServiceFilter(typeof(ProjectNotArchivedFilter))]
    [Route("/api/projects/{projectId}/boards/{boardId}/columns")]
    public class ColumnDefinitionController : ControllerBase
    {
        private readonly IColumnService _columnService;
        private readonly ILogger<ColumnDefinitionController> _logger;

        public ColumnDefinitionController(
            IColumnService columnService, 
            ILogger<ColumnDefinitionController> logger)
        {
            _columnService = columnService;
            _logger = logger;
        }

        [HttpPost]
        [Authorize(Policy = PolicyNames.ProjectAdmin)]
        [ProducesResponseType(typeof(ColumnResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ColumnResponseDto>> CreateColumnAsync(Guid projectId, Guid boardId, [FromBody] CreateColumnDto dto)
        {
            var response = await _columnService.CreateColumnAsync(projectId, boardId, dto);
            return Created(string.Empty, response);
        }

        [HttpDelete("{columnId}")]
        [Authorize(Policy = PolicyNames.ProjectAdmin)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> DeleteColumnAsync(Guid projectId, Guid boardId, Guid columnId)
        {
            await _columnService.DeleteColumnAsync(projectId, boardId, columnId);
            return NoContent();
        }

        [HttpGet]
        [Authorize(Policy = PolicyNames.ProjectViewer)]
        [ProducesResponseType(typeof(List<ColumnResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<ColumnResponseDto>>> GetColumnsAsync(Guid projectId, Guid boardId)
        {
            var response = await _columnService.GetColumnsAsync(projectId, boardId);
            return Ok(response);

        }

        [HttpPatch("{columnId}")]
        [Authorize(Policy = PolicyNames.ProjectAdmin)]
        [ProducesResponseType(typeof(ColumnResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ColumnResponseDto>> UpdateColumnAsync(Guid projectId, Guid boardId, Guid columnId, UpdateColumnDto dto)
        {
            var response = await _columnService.UpdateColumnAsync(projectId, boardId, columnId, dto);
            return Ok(response);
        }

        [HttpPost("reorder")]
        [Authorize(Policy = PolicyNames.ProjectAdmin)]
        [ProducesResponseType(typeof(List<ColumnResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<ColumnResponseDto>>> OrderColumnsAsync(Guid projectId, Guid boardId, [FromBody] List<ColumnOrderDto> order)
        {
            var response = await _columnService.OrderColumnsAsync(projectId, boardId, order);
            return Ok(response);
        }
    }
}
