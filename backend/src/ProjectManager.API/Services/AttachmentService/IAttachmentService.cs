using ProjectManager.API.DTOs.Attachment;

namespace ProjectManager.API.Services.AttachmentService
{
    public interface IAttachmentService
    {
        Task<AttachmentResponseDto> UploadTaskAttachmentAsync(Guid projectId, Guid taskId, Stream fileStream, string fileName, string contentType, long sizeBytes);
        Task<AttachmentResponseDto> UploadProjectAttachmentAsync(Guid projectId, Stream fileStream, string fileName, string contentType, long sizeBytes);
        Task<List<AttachmentResponseDto>> GetTaskAttachmentsAsync(Guid projectId, Guid taskId);
        Task<List<AttachmentResponseDto>> GetProjectAttachmentsAsync(Guid projectId);
        Task<(Stream stream, string fileName, string contentType)> DownloadAttachmentAsync(Guid projectId, Guid attachmentId);
        Task DeleteAttachmentAsync(Guid projectId, Guid attachmentId);
    }
}