using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManager.API.DTOs.Attachment;
using ProjectManager.API.Filters;
using ProjectManager.API.Services.AttachmentService;

namespace ProjectManager.API.Controllers
{
    [ApiController]
    [ServiceFilter(typeof(ProjectNotArchivedFilter))]
    [Route("api/projects/{projectId}/tasks/{taskId}/attachments")]
    public class TaskAttachmentController : ControllerBase
    {
        private readonly IAttachmentService _attachmentService;

        public TaskAttachmentController(IAttachmentService attachmentService)
        {
            _attachmentService = attachmentService;
        }

        [HttpGet]
        [Authorize(Policy = "ProjectViewer")]
        [ProducesResponseType(typeof(List<AttachmentResponseDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<AttachmentResponseDto>>> GetTaskAttachmentsAsync(Guid projectId, Guid taskId)
        {
            try
            {
                var response = await _attachmentService.GetTaskAttachmentsAsync(projectId, taskId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        
        [HttpPost]
        [Authorize(Policy = "ProjectMember")]
        [ProducesResponseType(typeof(AttachmentResponseDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<AttachmentResponseDto>> UploadTaskAttachmentAsync(Guid projectId, Guid taskId, IFormFile file)
        {
            try
            {
                using var stream = file.OpenReadStream();
                var response = await _attachmentService.UploadTaskAttachmentAsync(
                    projectId,
                    taskId,
                    stream,
                    file.FileName,
                    file.ContentType,
                    file.Length
                );
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("{attachmentId}/download")]
        [Authorize(Policy = "ProjectViewer")]
        public async Task<IActionResult> DownloadAttachmentAsync(Guid projectId, Guid taskId, Guid attachmentId)
        {
            try
            {
                var (stream, fileName, contentType) = await _attachmentService
                    .DownloadAttachmentAsync(projectId, attachmentId);
                return File(stream, contentType, fileName);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        
        [HttpDelete("{attachmentId}")]
        [Authorize(Policy = "ProjectMember")]
        public async Task<IActionResult> DeleteAttachmentAsync(Guid projectId, Guid taskId, Guid attachmentId)
        {
            try
            {
                await _attachmentService.DeleteAttachmentAsync(projectId, attachmentId);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
