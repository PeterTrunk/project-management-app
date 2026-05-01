using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Data;
using ProjectManager.API.DTOs.Attachment;
using ProjectManager.API.Model;
using ProjectManager.API.Services.AttachmentService;
using ProjectManager.API.Services.CurrentUserService;
using ProjectManager.API.Services.FileStorageService;

namespace ProjectManager.API.DTOs.Attachment
{
    public class AttachmentService : IAttachmentService
    {
        private readonly AppDbContext _context;
        private readonly IFileStorageService _fileStorageService;
        private readonly ICurrentUserService _currentUserService;
        
        public AttachmentService(AppDbContext context, IFileStorageService fileStorageService, ICurrentUserService currentUserService)
        {
            _context = context;
            _fileStorageService = fileStorageService;
            _currentUserService = currentUserService;
        }

        public async Task DeleteAttachmentAsync(Guid projectId, Guid attachmentId)
        {
            var attachment = await _context.Attachments
                .FirstOrDefaultAsync(a => a.Id == attachmentId && a.ProjectId == projectId);

            if (attachment == null)
                throw new Exception("Fájl nem található!");

            await _fileStorageService.DeleteFileAsync(attachment.StorageKey);

            _context.Attachments.Remove(attachment);
            await _context.SaveChangesAsync();
        }

        public async Task<(Stream stream, string fileName, string contentType)> DownloadAttachmentAsync(Guid projectId, Guid attachmentId)
        {
            var attachment = await _context.Attachments
                .FirstOrDefaultAsync(a => a.Id == attachmentId && a.ProjectId == projectId);

            if (attachment == null)
                throw new Exception("Fájl nem található!");

            var stream = await _fileStorageService.GetFileStreamAsync(attachment.StorageKey);

            return (stream, attachment.FileName, attachment.ContentType);
        }

        public async Task<List<AttachmentResponseDto>> GetProjectAttachmentsAsync(Guid projectId)
        {
            var attachments = await _context.Attachments
                .Where(a => a.ProjectId == projectId && a.TaskId == null)
                .Include(a => a.UploadedBy)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            return attachments.Select(MapToDto).ToList();
        }

        public async Task<List<AttachmentResponseDto>> GetTaskAttachmentsAsync(Guid projectId, Guid taskId)
        {
            var attachments = await _context.Attachments
                .Where(a => a.ProjectId == projectId && a.TaskId == taskId)
                .Include(a => a.UploadedBy)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            return attachments.Select(MapToDto).ToList();
        }

        public async Task<AttachmentResponseDto> UploadProjectAttachmentAsync(Guid projectId, Stream fileStream, string fileName, string contentType, long sizeBytes)
        {
            var storageKey = _fileStorageService.GenerateStorageKey(projectId, null, fileName);

            await _fileStorageService.UploadFileAsync(fileStream, fileName, contentType, storageKey);

            var attachment = new Model.Attachment
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                TaskId = null,
                UploadedById = _currentUserService.UserId,
                FileName = fileName,
                ContentType = contentType,
                SizeBytes = sizeBytes,
                StorageKey = storageKey,
                AttachmentType = GetAttachmentType(contentType),
                CreatedAt = DateTime.UtcNow
            };

            await _context.Attachments.AddAsync(attachment);
            await _context.SaveChangesAsync();

            return MapToDto(attachment);
        }

        public async Task<AttachmentResponseDto> UploadTaskAttachmentAsync(Guid projectId, Guid taskId, Stream fileStream, string fileName, string contentType, long sizeBytes)
        {
            var storageKey = _fileStorageService.GenerateStorageKey(projectId, taskId, fileName);

            await _fileStorageService.UploadFileAsync(fileStream, fileName, contentType, storageKey);

            var attachment = new Model.Attachment
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                TaskId = taskId,
                UploadedById = _currentUserService.UserId,
                FileName = fileName,
                ContentType = contentType,
                SizeBytes = sizeBytes,
                StorageKey = storageKey,
                AttachmentType = GetAttachmentType(contentType),
                CreatedAt = DateTime.UtcNow
            };

            await _context.Attachments.AddAsync(attachment);
            await _context.SaveChangesAsync();

            return MapToDto(attachment);
        }

        private AttachmentResponseDto MapToDto(Model.Attachment attachment)
        {
            return new AttachmentResponseDto
            {
                Id = attachment.Id,
                ProjectId = attachment.ProjectId,
                TaskId = attachment.TaskId,
                FileName = attachment.FileName,
                ContentType = attachment.ContentType,
                SizeBytes = attachment.SizeBytes,
                AttachmentType = attachment.AttachmentType,
                UploadedByName = attachment.UploadedBy?.DisplayName ?? "Ismeretlen",
                CreatedAt = attachment.CreatedAt
            };
        }

        private string GetAttachmentType(string contentType)
        {
            if (contentType.StartsWith("image/")) return "image";
            if (contentType == "application/pdf") return "pdf";
            if (contentType.Contains("spreadsheet") || contentType.Contains("excel")) return "spreadsheet";
            if (contentType.Contains("document") || contentType.Contains("word")) return "document";
            return "other";
        }
    }
}
