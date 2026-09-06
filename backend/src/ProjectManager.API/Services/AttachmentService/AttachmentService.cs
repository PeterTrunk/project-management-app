using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.Common.Options;
using ProjectManager.API.Common.Exceptions;
using ProjectManager.API.Data;
using ProjectManager.API.DTOs.Attachment;
using ProjectManager.API.Hubs;
using ProjectManager.API.Model;
using ProjectManager.API.Services.ActivityService;
using ProjectManager.API.Services.CurrentUserService;
using ProjectManager.API.Services.FileStorageService;
using ProjectManager.API.Services.RateLimit;

namespace ProjectManager.API.Services.AttachmentService
{
    public class AttachmentService : IAttachmentService
    {
        private readonly AppDbContext _context;
        private readonly IFileStorageService _fileStorageService;
        private readonly IActivityService _activityService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IHubContext<ProjectHub> _hubContext;
        private readonly ILogger<AttachmentService> _logger;
        private readonly AttachmentOptions _attachmentOptions;
        private readonly IRateLimitService _rateLimitService;

        public AttachmentService(
            AppDbContext context,
            IFileStorageService fileStorageService,
            IActivityService activityService,
            ICurrentUserService currentUserService,
            IHubContext<ProjectHub> hubContext,
            ILogger<AttachmentService> logger,
            IOptions<AttachmentOptions> attachmentOptions,
            IRateLimitService rateLimitService)
        {
            _context = context;
            _fileStorageService = fileStorageService;
            _activityService = activityService;
            _currentUserService = currentUserService;
            _hubContext = hubContext;
            _logger = logger;
            _attachmentOptions = attachmentOptions.Value;
            _rateLimitService = rateLimitService;
        }

        public async Task DeleteAttachmentAsync(Guid projectId, Guid attachmentId)
        {
            var attachment = await _context.Attachments
                .FirstOrDefaultAsync(a => a.Id == attachmentId && a.ProjectId == projectId);

            if (attachment == null)
                throw new NotFoundException("Fájl nem található!");

            await _fileStorageService.DeleteFileAsync(attachment.StorageKey);
            _logger.LogInformation("Fájl törölve | AttachmentId: {AttachmentId} | StorageKey: {StorageKey}", attachmentId, attachment.StorageKey);

            _context.Attachments.Remove(attachment);
            await _context.SaveChangesAsync();

            try
            {
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("AttachmentDeleted", new
                    {
                        attachmentId = attachment.Id,
                        projectId = attachment.ProjectId,
                        taskId = attachment.TaskId
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                    "AttachmentDeleted", projectId);
            }

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
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: ActivityCreated | ProjectId: {ProjectId}", projectId);
            }
        }

        public async Task DownloadAttachmentAsync(Guid projectId, Guid attachmentId, Stream destination, CancellationToken ct = default)
        {
            var attachment = await _context.Attachments
                .FirstOrDefaultAsync(a => a.Id == attachmentId && a.ProjectId == projectId);

            if (attachment == null)
                throw new NotFoundException("Fájl nem található!");

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
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: ActivityCreated | ProjectId: {ProjectId}", projectId);
            }

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
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: ActivityCreated | ProjectId: {ProjectId}", projectId);
            }

            return MapToDto(attachment);
        }

        public async Task<PresignedUrlResponseDto> GetPresignedUploadUrlAsync(Guid projectId, Guid? taskId, PresignedUrlRequestDto dto)
        {
            //A presigned PUT-nak nincs szerveroldali méretkorlátja, és a tényleges kikényszerítés csak a confirm lépésben történik.
            //Akkor viszont a fájl már fent van. Aki nem hívja meg a confirmot, annak az objektuma a
            //Cleanup job következő futásáig marad. A rate limit ezt a ciklust töri meg.
            var (isLimited, retryAfter) = await _rateLimitService.IsRateLimitedAsync(
                $"presigned_upload:{_currentUserService.UserId}", 60, TimeSpan.FromMinutes(10));
            if (isLimited)
            {
                _logger.LogWarning("Rate limit elérve presigned URL igénylésnél | UserId: {UserId}", _currentUserService.UserId);
                throw new RateLimitException($"Túl sok feltöltési kérés. Próbáld újra {retryAfter} másodperc múlva!");
            }

            //Validáció
            ValidateFile(dto.ContentType, dto.SizeBytes);

            var storageKey = _fileStorageService.GenerateStorageKey(projectId, taskId, dto.FileName);
            var expiresAt = DateTime.UtcNow.AddSeconds(120);

            var presignedUrl = await _fileStorageService.GeneratePresignedPutUrlAsync(
                storageKey, dto.ContentType);

            //PresignedUrlLog létrehozása
            var log = new PresignedUrlLog
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                TaskId = taskId,
                StorageKey = storageKey,
                FileName = dto.FileName,
                ContentType = dto.ContentType,
                SizeBytes = dto.SizeBytes,
                ExpiresAt = expiresAt,
                Confirmed = false,
                CreatedAt = DateTime.UtcNow,
                CreatedById = _currentUserService.UserId
            };

            await _context.PresignedUrlLogs.AddAsync(log);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Presigned URL generálva | StorageKey: {StorageKey} | FileName: {FileName}", storageKey, dto.FileName);

            return new PresignedUrlResponseDto
            {
                PresignedUrl = presignedUrl,
                StorageKey = storageKey,
                ExpiresAt = expiresAt
            };
        }

        public async Task<AttachmentResponseDto> ConfirmUploadAsync(Guid projectId, Guid? taskId, ConfirmUploadDto dto)
        {
            //PresignedUrlLog keresése
            var log = await _context.PresignedUrlLogs
                .FirstOrDefaultAsync(p => p.StorageKey == dto.StorageKey
                                        && p.ProjectId == projectId);
            if (log == null)
                throw new ValidationException("Érvénytelen storage key!");

            //Lejárt-e?
            if (log.ExpiresAt < DateTime.UtcNow)
            {
                _logger.LogWarning("Lejárt presigned URL confirm kísérlet | StorageKey: {StorageKey}", dto.StorageKey);
                throw new ValidationException("A feltöltési URL lejárt!");
            }

            //Duplikált confirm ellenőrzés
            if (log.Confirmed)
                throw new ConflictException("Ez a fájl már meg lett erősítve!");

            //Duplikált Attachment ellenőrzés (unique constraint előtt)
            if (await _context.Attachments.AnyAsync(a => a.StorageKey == dto.StorageKey))
                throw new ConflictException("Ez a fájl már fel lett töltve!");

            //MinIO-ban létezik-e ténylegesen?
            var objectInfo = await _fileStorageService.GetObjectInfoAsync(dto.StorageKey);
            if (objectInfo == null)
                throw new NotFoundException("A fájl nem található a tárolóban!");

            //Méret ellenőrzés
            if (objectInfo.Size > log.SizeBytes * 1.1) // 10% tolerancia
                throw new ValidationException("A fájl mérete nem egyezik!");

            //A ténylegesen feltöltött objektum típusa is egyezzen a bejelentettel.
            //A presigned URL már aláírásba kötötte a Content-Type-ot, ez a második
            //védelmi réteg - és ez kerül az adatbázisba, nem a kliens bejelentése.
            var storedContentType = objectInfo.ContentType;
            if (!string.IsNullOrEmpty(storedContentType)
                && !string.Equals(storedContentType, log.ContentType, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Content-Type eltérés a feltöltésnél | StorageKey: {StorageKey} | Bejelentett: {Declared} | Tárolt: {Stored}",
                    dto.StorageKey, log.ContentType, storedContentType);
                throw new ValidationException("A fájl típusa nem egyezik a bejelentettel!");
            }

            //A route-ból érkező taskId felülírhatja a naplózottat, ezért ellenőrizni kell, hogy tényleg ehhez a projekthez tartozik-e
            var effectiveTaskId = taskId ?? log.TaskId;
            if (effectiveTaskId.HasValue)
            {
                var taskBelongsToProject = await _context.ProjectTasks
                    .AnyAsync(t => t.Id == effectiveTaskId.Value && t.ProjectId == projectId);
                if (!taskBelongsToProject)
                    throw new NotFoundException("Task nem található!");
            }

            //Csak az kérheti a megerősítést, aki a presigned URL-t kérte.
            //Enélkül a projekt bármely tagja megerősíthetné más függőben lévő feltöltését, és a fájl az ő nevén jelenne meg.
            if (log.CreatedById != _currentUserService.UserId)
            {
                _logger.LogWarning(
                    "Idegen feltöltés megerősítési kísérlete | StorageKey: {StorageKey} | Kérő: {RequesterId} | Megerősítő: {ConfirmerId}",
                    dto.StorageKey, log.CreatedById, _currentUserService.UserId);
                throw new ForbiddenException("Ezt a feltöltést nem te kezdeményezted!");
            }

            //Attachment betöltése UploadedBy-jal
            var uploadedBy = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == _currentUserService.UserId);

            //Log megjelölése confirmált-ként
            log.Confirmed = true;

            //Verzió meghatározás + mentés retry logikával
            var maxRetries = 3;
            Attachment? attachment = null;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    var existingCount = await _context.Attachments
                        .Where(a => a.ProjectId == projectId && a.FileName == log.FileName)
                        .CountAsync();

                    attachment = new Attachment
                    {
                        Id = Guid.NewGuid(),
                        ProjectId = projectId,
                        TaskId = effectiveTaskId,
                        UploadedById = _currentUserService.UserId,
                        UploadedBy = uploadedBy!,
                        FileName = log.FileName,
                        ContentType = log.ContentType,
                        SizeBytes = objectInfo.Size,
                        StorageKey = log.StorageKey,
                        AttachmentType = GetAttachmentType(log.ContentType),
                        Version = existingCount,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _context.Attachments.AddAsync(attachment);
                    await _context.SaveChangesAsync();
                    break;
                }
                catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("unique") == true)
                {
                    _context.ChangeTracker.Clear();
                    log.Confirmed = true; //reset after clear
                    if (attempt == maxRetries - 1)
                        throw new ValidationException("Nem sikerült menteni a fájlt, kérjük próbálja újra!");
                }
            }

            _logger.LogInformation("Fájl feltöltés megerősítve | StorageKey: {StorageKey} | FileName: {FileName}", attachment!.StorageKey, attachment.FileName);

            try
            {
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("AttachmentUploaded", new
                    {
                        attachmentId = attachment.Id,
                        projectId = attachment.ProjectId,
                        taskId = attachment.TaskId,
                        fileName = attachment.FileName,
                        contentType = attachment.ContentType,
                        sizeBytes = attachment.SizeBytes,
                        attachmentType = attachment.AttachmentType,
                        uploadedByName = attachment.UploadedBy?.DisplayName ?? "",
                        version = attachment.Version,
                        createdAt = attachment.CreatedAt
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                    "AttachmentUploaded", projectId);
            }

            //Activity log
            try
            {
                var activity = await _activityService.LogActivityAsync(
                    projectId,
                    taskId.HasValue ? "Task" : "Project",
                    taskId ?? projectId,
                    "AttachmentUploaded",
                    $"{_currentUserService.DisplayName} feltöltötte a {log.FileName} fájlt"
                );
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("ActivityCreated", activity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: ActivityCreated | ProjectId: {ProjectId}", projectId);
            }

            return MapToDto(attachment);
        }

        public async Task<AttachmentResponseDto?> GetAttachmentMetadataAsync(Guid projectId, Guid attachmentId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new NotFoundException("Projekt nem található!");

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
                Version = attachment.Version,
                CreatedAt = attachment.CreatedAt
            };
        }

        private string GetAttachmentType(string contentType)
        {
            if (contentType.StartsWith("image/")) return AttachmentTypes.Image;
            if (contentType == "application/pdf") return AttachmentTypes.Pdf;
            if (contentType.Contains("spreadsheet") || contentType.Contains("excel")) return AttachmentTypes.Spreadsheet;
            if (contentType.Contains("document") || contentType.Contains("word")) return AttachmentTypes.Document;
            return AttachmentTypes.Other;
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
            var maxSizeMb = _attachmentOptions.MaxUploadSizeMb;
            var maxSizeBytes = (long)maxSizeMb * 1024 * 1024;

            if (sizeBytes > maxSizeBytes)
            {
                _logger.LogWarning("Fájl méret limit túllépve | Size: {SizeBytes} | Limit: {MaxSizeBytes}", sizeBytes, maxSizeBytes);
                throw new ValidationException($"A fájl mérete meghaladja a {maxSizeMb}MB limitet!");
            }
            
            if (!AllowedContentTypes.Contains(contentType))
            {
                _logger.LogWarning("Nem engedélyezett fájltípus | ContentType: {ContentType}", contentType);
                throw new ValidationException($"A {contentType} fájltípus nem engedélyezett!");
            }
                
        }
    }
}
