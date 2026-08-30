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

        public BoardController(IBoardService boardService)
        {
            _boardService = boardService;
        }

        [HttpPost]
        [Authorize(Policy = PolicyNames.ProjectAdmin)]
        [ProducesResponseType(typeof(BoardResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<BoardResponseDto>> CreateBoardAsync(Guid projectId, [FromBody] CreateBoardDto dto)
        {
            try
            {
                var response = await _boardService.CreateBoardAsync(projectId, dto);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{boardId}")]
        [Authorize(Policy = PolicyNames.ProjectAdmin)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> DeleteBoardAsync(Guid projectId, Guid boardId)
        {
            try
            {
                await _boardService.DeleteBoardAsync(projectId, boardId);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        [Authorize(Policy = PolicyNames.ProjectViewer)]
        [ProducesResponseType(typeof(List<BoardResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<BoardResponseDto>>> GetBoardsAsync(
            Guid projectId,
            [FromQuery] string? scope = null)
        {
            try
            {
                var response = await _boardService.GetBoardsAsync(projectId, scope);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPatch("{boardId}")]
        [Authorize(Policy = PolicyNames.ProjectAdmin)]
        [ProducesResponseType(typeof(BoardResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<BoardResponseDto>> UpdateBoardAsync(Guid projectId, Guid boardId, [FromBody] UpdateBoardDto dto)
        {
            try
            {
                var response = await _boardService.UpdateBoardAsync(projectId, boardId, dto);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
