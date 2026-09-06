using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.DTOs.Boards;
using ProjectManager.API.Filters;
using ProjectManager.API.Services.BoardService;

namespace ProjectManager.API.Controllers
{
    [ApiController]
    [ServiceFilter(typeof(ProjectNotArchivedFilter))]
    [Route("/api/projects/{projectId}/boards")]
    public class BoardController : ControllerBase
    {
        private readonly IBoardService _boardService;
        private readonly ILogger<BoardController> _logger;

        public BoardController(
            IBoardService boardService,
            ILogger<BoardController> logger)
        {
            _boardService = boardService;
            _logger = logger;
        }

        [HttpPost]
        [Authorize(Policy = PolicyNames.ProjectAdmin)]
        [ProducesResponseType(typeof(BoardResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<BoardResponseDto>> CreateBoardAsync(Guid projectId, [FromBody] CreateBoardDto dto)
        {
            var response = await _boardService.CreateBoardAsync(projectId, dto);
            return Ok(response);
        }

        [HttpDelete("{boardId}")]
        [Authorize(Policy = PolicyNames.ProjectAdmin)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> DeleteBoardAsync(Guid projectId, Guid boardId)
        {
            await _boardService.DeleteBoardAsync(projectId, boardId);
            return NoContent();
        }

        [HttpGet]
        [Authorize(Policy = PolicyNames.ProjectViewer)]
        [ProducesResponseType(typeof(List<BoardResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<BoardResponseDto>>> GetBoardsAsync(
            Guid projectId,
            [FromQuery] string? scope = null)
        {
            var response = await _boardService.GetBoardsAsync(projectId, scope);
            return Ok(response);
        }

        [HttpPatch("{boardId}")]
        [Authorize(Policy = PolicyNames.ProjectAdmin)]
        [ProducesResponseType(typeof(BoardResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<BoardResponseDto>> UpdateBoardAsync(Guid projectId, Guid boardId, [FromBody] UpdateBoardDto dto)
        {
            var response = await _boardService.UpdateBoardAsync(projectId, boardId, dto);
            return Ok(response);
        }
    }
}
