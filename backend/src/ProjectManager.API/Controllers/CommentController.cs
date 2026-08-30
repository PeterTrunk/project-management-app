using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.DTOs.Comments;
using ProjectManager.API.Filters;
using ProjectManager.API.Services.CommentService;

namespace ProjectManager.API.Controllers
{
    [ApiController]
    [ServiceFilter(typeof(ProjectNotArchivedFilter))]
    [Route("api/projects/{projectId}/tasks/{taskId}/comments")]
    public class CommentController : ControllerBase
    {
        private readonly ICommentService _commentService;

        public CommentController(ICommentService commentService)
        {
            _commentService = commentService;
        }

        [HttpGet]
        [Authorize(Policy = PolicyNames.ProjectViewer)]
        [ProducesResponseType(typeof(List<CommentResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<CommentResponseDto>>> GetCommentsAsync(Guid projectId, Guid taskId)
        {
            try
            {
                var response = await _commentService.GetCommentsAsync(projectId, taskId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        [Authorize(Policy = PolicyNames.ProjectMember)]
        [ProducesResponseType(typeof(CommentResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<CommentResponseDto>> CommentOnTaskAsync(Guid projectId, Guid taskId, [FromBody] CreateCommentDto dto)
        {
            try
            {
                var response = await _commentService.CommentOnTaskAsync(projectId, taskId, dto);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{commentId}")]
        [Authorize(Policy = PolicyNames.ProjectMember)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> DeleteCommentFromTaskAsync(Guid projectId, Guid taskId, Guid commentId)
        {
            try
            {
                await _commentService.DeleteCommentFromTaskAsync(projectId, taskId, commentId);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
