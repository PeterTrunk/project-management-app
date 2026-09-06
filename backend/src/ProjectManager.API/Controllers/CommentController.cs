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
        private readonly ILogger<CommentController> _logger;

        public CommentController(
            ICommentService commentService,
            ILogger<CommentController> logger)
        {
            _commentService = commentService;
            _logger = logger;
        }

        [HttpGet]
        [Authorize(Policy = PolicyNames.ProjectViewer)]
        [ProducesResponseType(typeof(List<CommentResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<CommentResponseDto>>> GetCommentsAsync(Guid projectId, Guid taskId)
        {
            var response = await _commentService.GetCommentsAsync(projectId, taskId);
            return Ok(response);
        }

        [HttpPost]
        [Authorize(Policy = PolicyNames.ProjectMember)]
        [ProducesResponseType(typeof(CommentResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<CommentResponseDto>> CommentOnTaskAsync(Guid projectId, Guid taskId, [FromBody] CreateCommentDto dto)
        {
            var response = await _commentService.CommentOnTaskAsync(projectId, taskId, dto);
            return Ok(response);
        }

        [HttpDelete("{commentId}")]
        [Authorize(Policy = PolicyNames.ProjectMember)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> DeleteCommentFromTaskAsync(Guid projectId, Guid taskId, Guid commentId)
        {
            await _commentService.DeleteCommentFromTaskAsync(projectId, taskId, commentId);
            return NoContent();
        }
    }
}
