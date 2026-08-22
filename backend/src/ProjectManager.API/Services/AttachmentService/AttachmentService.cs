using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.Data;
using ProjectManager.API.DTOs.Attachment;
using ProjectManager.API.Hubs;
using ProjectManager.API.Model;
using ProjectManager.API.Services.ActivityService;
using ProjectManager.API.Services.CurrentUserService;
using ProjectManager.API.Services.FileStorageService;

namespace ProjectManager.API.Services.AttachmentService
{
    public class AttachmentService : IAttachmentService
    {
        private readonly AppDbContext _context;
        private readonly IFileStorageService _fileStorageService;
        private readonly IActivityService _activityService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IHubContext<ProjectHub> _hubContext;
        
        public AttachmentService(AppDbContext context, IFileStorageService fileStorageService, IActivityService activityService, ICurrentUserService currentUserService, IHubContext<ProjectHub> hubContext)
        {
            _context = context;
            _fileStorageService = fileStorageService;
            _activityService = activityService;
            _currentUserService = currentUserService;
            _hubContext = hubContext;
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

            try
            {
                var activity = await _activityService.LogActivityAsync(
                    attachment.TaskId.HasValue ? attachment.ProjectId : attachment.ProjectId,
                    attachment.TaskId.HasValue ? "Task" : "Project",
                    attachment.TaskId ?? attachment.ProjectId,
                    "AttachmentDeleted",
                    $"{_currentUserService.DisplayName} törölte a {attachment.FileName} fájlt"
                );
                await _hubContext.Clients
                    .Group($"project-{attachment.ProjectId}")
                    .SendAsync("ActivityCreated", activity);
            }
            catch { }
        }

        public async Task DownloadAttachmentAsync(Guid projectId, Guid attachmentId, Stream destination, CancellationToken ct = default)
        {
            var attachment = await _context.Attachments
                .FirstOrDefaultAsync(a => a.Id == attachmentId && a.ProjectId == projectId);

            if (attachment == null)
                throw new Exception("Fájl nem található!");

            await _fileStorageService.StreamFileAsync(attachment.StorageKey, destination, ct);
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
            ValidateFile(contentType, sizeBytes);

            var storageKey = _fileStorageService.GenerateStorageKey(projectId, null, fileName);

            await _fileStorageService.UploadFileAsync(fileStream, fileName, contentType, storageKey);

            var attachment = new Attachment
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

            try
            {
                var activity = await _activityService.LogActivityAsync(
                    projectId,
                    "Project",
                    projectId,
                    "AttachmentUploaded",
                    $"{_currentUserService.DisplayName} feltöltötte a {fileName} fájlt a projekt dokumentumok közé"
                );
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("ActivityCreated", activity);
            }
            catch { }
            
            return MapToDto(attachment);
        }

        public async Task<AttachmentResponseDto> UploadTaskAttachmentAsync(Guid projectId, Guid taskId, Stream fileStream, string fileName, string contentType, long sizeBytes)
        {
            ValidateFile(contentType, sizeBytes);

            var storageKey = _fileStorageService.GenerateStorageKey(projectId, taskId, fileName);

            await _fileStorageService.UploadFileAsync(fileStream, fileName, contentType, storageKey);

            var attachment = new Attachment
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

            try
            {
                var activity = await _activityService.LogActivityAsync(
                    projectId,
                    "Task",
                    taskId,
                    "AttachmentUploaded",
                    $"{_currentUserService.DisplayName} feltöltötte a {fileName} fájlt"
                );
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("ActivityCreated", activity);
            }
            catch { }

            return MapToDto(attachment);
        }

        public async Task<AttachmentResponseDto?> GetAttachmentMetadataAsync(Guid projectId, Guid attachmentId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található!");

            var attachment = await _context.Attachments
                .Include(a => a.UploadedBy)
                .FirstOrDefaultAsync(a => a.Id == attachmentId && a.ProjectId == projectId);

            if (attachment == null)
                return null;

            return MapToDto(attachment);
        }

        private AttachmentResponseDto MapToDto(Attachment attachment)
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
            if (contentType.StartsWith("image/")) return AttachmentType.Image;
            if (contentType == "application/pdf") return AttachmentType.Pdf;
            if (contentType.Contains("spreadsheet") || contentType.Contains("excel")) return AttachmentType.Spreadsheet;
            if (contentType.Contains("document") || contentType.Contains("word")) return AttachmentType.Document;
            return AttachmentType.Other;
        }

        private static readonly HashSet<string> AllowedContentTypes = new()
        {
            //Képek
            "image/jpeg",
            "image/png",
            "image/gif",
            "image/webp",
            //Dokumentumok
            "application/pdf",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            //Táblázatok
            "application/vnd.ms-excel",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            //Szöveg
            "text/plain",
            //Archívum
            "application/zip",
            "application/x-zip-compressed"
        };

        private void ValidateFile(string contentType, long sizeBytes)
        {
            var maxSizeMb = int.Parse(
                Environment.GetEnvironmentVariable("MAX_UPLOAD_SIZE_MB") ?? "64");
            var maxSizeBytes = maxSizeMb * 1024 * 1024;

            if (sizeBytes > maxSizeBytes)
                throw new Exception($"A fájl mérete meghaladja a {maxSizeMb}MB limitet!");

            if (!AllowedContentTypes.Contains(contentType))
                throw new Exception($"A {contentType} fájltípus nem engedélyezett!");
        }
    }
}
