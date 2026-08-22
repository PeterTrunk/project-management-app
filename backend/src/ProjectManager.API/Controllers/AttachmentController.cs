using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManager.API.DTOs.Attachment;
using ProjectManager.API.Filters;
using ProjectManager.API.Services.AttachmentService;

namespace ProjectManager.API.Controllers
{
    [ApiController]
    [ServiceFilter(typeof(ProjectNotArchivedFilter))]
    [Route("api/projects/{projectId}")]
    public class AttachmentController : ControllerBase
    {
        private readonly IAttachmentService _attachmentService;

        public AttachmentController(IAttachmentService attachmentService)
        {
            _attachmentService = attachmentService;
        }

        //Projekt szintű lista
        [HttpGet("attachments")]
        [Authorize(Policy = "ProjectViewer")]
        [ProducesResponseType(typeof(List<AttachmentResponseDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<AttachmentResponseDto>>> GetProjectAttachmentsAsync(Guid projectId)
        {
            try
            {
                var response = await _attachmentService.GetProjectAttachmentsAsync(projectId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        //Task szintű lista
        [HttpGet("tasks/{taskId}/attachments")]
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

        //Projekt feltöltés
        [HttpPost("attachments")]
        [Authorize(Policy = "ProjectMember")]
        [ProducesResponseType(typeof(AttachmentResponseDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<AttachmentResponseDto>> UploadProjectAttachmentAsync(Guid projectId, IFormFile file)
        {
            try
            {
                using var stream = file.OpenReadStream();
                var response = await _attachmentService.UploadProjectAttachmentAsync(
                    projectId,
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

        //Task feltöltés
        [HttpPost("tasks/{taskId}/attachments")]
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

        //Letöltés (projekt és task szintű egyaránt)
        [HttpGet("attachments/{attachmentId}/download")]
        [Authorize(Policy = "ProjectViewer")]
        public async Task<IActionResult> DownloadAttachmentAsync(Guid projectId, Guid attachmentId, CancellationToken ct)
        {
            try
            {
                var attachment = await _attachmentService.GetAttachmentMetadataAsync(projectId, attachmentId);
                if (attachment == null)
                    return NotFound();

                Response.Headers["Content-Disposition"] = $"attachment; filename=\"{attachment.FileName}\"";
                Response.Headers["X-Content-Type-Options"] = "nosniff";
                Response.ContentType = attachment.ContentType;

                await _attachmentService.DownloadAttachmentAsync(projectId, attachmentId, Response.Body, ct);

                return new EmptyResult();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        //Törlés (projekt és task szintű egyaránt)
        [HttpDelete("attachments/{attachmentId}")]
        [Authorize(Policy = "ProjectMember")]
        public async Task<IActionResult> DeleteAttachmentAsync(Guid projectId, Guid attachmentId)
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

        //Task szintű presigned URL
        [HttpPost("tasks/{taskId}/attachments/presigned")]
        [Authorize(Policy = "ProjectMember")]
        [ProducesResponseType(typeof(PresignedUrlResponseDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<PresignedUrlResponseDto>> GetTaskPresignedUploadUrlAsync(
            Guid projectId, 
            Guid taskId, 
            [FromBody] PresignedUrlRequestDto dto)
        {
            try
            {
                var response = await _attachmentService.GetPresignedUploadUrlAsync(projectId, taskId, dto);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        //Projekt szintű presigned URL
        [HttpPost("attachments/presigned")]
        [Authorize(Policy = "ProjectMember")]
        [ProducesResponseType(typeof(PresignedUrlResponseDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<PresignedUrlResponseDto>> GetProjectPresignedUploadUrlAsync(
            Guid projectId, 
            [FromBody] PresignedUrlRequestDto dto)
        {
            try
            {
                var response = await _attachmentService.GetPresignedUploadUrlAsync(projectId, null, dto);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        //Task szintű confirm
        [HttpPost("tasks/{taskId}/attachments/confirm")]
        [Authorize(Policy = "ProjectMember")]
        [ProducesResponseType(typeof(AttachmentResponseDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<AttachmentResponseDto>> ConfirmTaskUploadAsync(
            Guid projectId, Guid taskId, [FromBody] ConfirmUploadDto dto)
        {
            try
            {
                var response = await _attachmentService.ConfirmUploadAsync(projectId, taskId, dto);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        //Projekt szintű confirm
        [HttpPost("attachments/confirm")]
        [Authorize(Policy = "ProjectMember")]
        [ProducesResponseType(typeof(AttachmentResponseDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<AttachmentResponseDto>> ConfirmProjectUploadAsync(
            Guid projectId, [FromBody] ConfirmUploadDto dto)
        {
            try
            {
                var response = await _attachmentService.ConfirmUploadAsync(projectId, null, dto);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}