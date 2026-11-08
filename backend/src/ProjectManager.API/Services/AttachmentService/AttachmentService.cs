using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.Common.Data;
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
                    $"{{actor}} törölte a {attachment.FileName} fájlt"
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
            //A projekt ÖSSZES csatolmánya, a feladatokhoz tartozókkal együtt - a korábbi
            //`a.TaskId == null` szűrő kivezetve. A "projekt-szintű" nézet innentől kliensoldali
            //szűrés (`taskId === null`), ugyanaz a feltétel, ami az adatbázisban is.
            //
            //Indok: a hívó eddig a projekt-szintű csatolmányok MELLETT a teljes feladatgráfot is
            //letöltötte, csak hogy a feladatokhoz tartozókat megkapja - és ezt minden
            //csatolmány-eseményre újra. Egy lekérés ugyanazt adja, feladatgráf nélkül.
            //Ugyanaz a szerkezet, mint a git hivatkozások egyesített lekérésénél.
            var attachments = await _context.Attachments
                .Where(a => a.ProjectId == projectId)
                .Include(a => a.UploadedBy)
                .Include(a => a.ProjectTask)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            return attachments.Select(a => MapToDto(a)).ToList();
        }

        public async Task<List<AttachmentResponseDto>> GetTaskAttachmentsAsync(Guid projectId, Guid taskId)
        {
            var attachments = await _context.Attachments
                .Where(a => a.ProjectId == projectId && a.TaskId == taskId)
                .Include(a => a.UploadedBy)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            return attachments.Select(a => MapToDto(a)).ToList();
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
                    $"{{actor}} feltöltötte a {fileName} fájlt a projekt dokumentumok közé"
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
                    $"{{actor}} feltöltötte a {fileName} fájlt"
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
            //Az aláírt policy a típust és a méretet kikényszeríti, de aki a confirmot nem hívja meg,
            //annak az objektuma a Cleanup job következő futásáig a tárolóban marad.
            //A rate limit ezt a ciklust töri meg.
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

            var upload = await _fileStorageService.GeneratePresignedUploadAsync(
                storageKey, dto.ContentType, dto.SizeBytes);

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
                UploadUrl = upload.Url,
                Fields = upload.Fields,
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

            //A tárolóban létezik-e ténylegesen?
            var objectInfo = await _fileStorageService.GetObjectInfoAsync(dto.StorageKey);
            if (objectInfo == null)
                throw new NotFoundException("A fájl nem található a tárolóban!");

            //Méret ellenőrzés
            if (objectInfo.Size > log.SizeBytes * 1.1) // 10% tolerancia
                throw new ValidationException("A fájl mérete nem egyezik!");

            //A ténylegesen feltöltött objektum típusa egyezzen a bejelentettel.
            //
            //Ez MÁSODIK védelmi réteg: az aláírt POST policy óta a tároló maga is elutasítja
            //az eltérő típusú (403) és méretű (400) feltöltést - lásd
            //S3FileStorageService.GeneratePresignedUploadAsync. Az ellenőrzés mégis itt marad,
            //mert a kikényszerítés a tároló viselkedésén nyugszik, az pedig csere vagy
            //verzióváltás után némán megváltozhat; itt a sor létre sem jön.
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
            
            var uploadedBy = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == _currentUserService.UserId);

            //A MEGERŐSÍTÉS IGÉNYLÉSE, feltételes UPDATE-tel.
            //
            //Két dolgot ad a korábbi `log.Confirmed = true` értékadás helyett.
            //
            //Egyrészt ATOMI: a fenti `if (log.Confirmed)` ellenőrzés önmagában olvasás és
            //írás között állt, tehát két egyidejű megerősítés mindkettő szabadnak látta a
            //naplósort. Az invariánst eddig csak az `Attachments.StorageKey` egyedi indexe
            //tartotta, félrevezető hibaüzenettel. Ez a feltételes UPDATE ugyanaz a minta,
            //mint a refresh token rotációja: a döntés az érintett sorok SZÁMÁBÓL születik.
            //
            //Másrészt AZONNAL KIÍRÓDIK, és ez a lényegesebb. Korábban a jelölés a
            //`SaveChanges`-re várt, a hibaágon pedig a `ChangeTracker.Clear()` UTÁN kapott
            //értéket - vagyis egy leválasztott entitáson, tehát sosem jutott el az
            //adatbázisba. A takarító feladat a meg nem erősített naplósorokat a hozzájuk
            //tartozó FÁJLLAL együtt viszi el: egy élő csatolmány fájlja tűnt volna el a
            //tárolóból 15 perccel később, miközben az adatbázis szerint minden rendben van.
            var claimed = await _context.PresignedUrlLogs
                .Where(p => p.Id == log.Id && !p.Confirmed)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Confirmed, true));

            if (claimed == 0)
            {
                _logger.LogWarning(
                    "Egyidejű megerősítési kísérlet | StorageKey: {StorageKey} | UserId: {UserId}",
                    dto.StorageKey, _currentUserService.UserId);
                throw new ConflictException("Ez a fájl már meg lett erősítve!");
            }

            //Verzió meghatározás + mentés retry logikával
            const int maxRetries = 3;

            //A MENTETT csatolmány. Szándékosan külön a ciklusban épített példánytól: ha a
            //`SaveChanges` dob, a példány létezik, de nincs az adatbázisban - egy közös
            //változó tehát "sikernek" látszana. A siker egyetlen jele az, hogy a mentés visszatért.
            Attachment? saved = null;
            Exception? lastConflict = null;

            try
            {
                for (int attempt = 0; attempt < maxRetries; attempt++)
                {
                    var existingCount = await _context.Attachments
                        .Where(a => a.ProjectId == projectId && a.FileName == log.FileName)
                        .CountAsync();

                    var candidate = new Attachment
                    {
                        Id = Guid.NewGuid(),
                        ProjectId = projectId,
                        TaskId = effectiveTaskId,
                        UploadedById = _currentUserService.UserId,
                        FileName = log.FileName,
                        ContentType = log.ContentType,
                        SizeBytes = objectInfo.Size,
                        StorageKey = log.StorageKey,
                        AttachmentType = GetAttachmentType(log.ContentType),
                        Version = existingCount,
                        CreatedAt = DateTime.UtcNow
                    };

                    try
                    {
                        await _context.Attachments.AddAsync(candidate);
                        await _context.SaveChangesAsync();
                        saved = candidate;
                        break;
                    }
                    catch (Exception ex) when (ConcurrencyErrors.IsConflict(ex))
                    {
                        //A verziószám ütközött: egy másik feltöltés ugyanazzal a fájlnévvel
                        //kapta meg ugyanazt a sorszámot. Új kísérlet friss darabszámmal.
                        _context.ChangeTracker.Clear();
                        lastConflict = ex;
                    }
                }

                if (saved == null)
                {
                    _logger.LogWarning(lastConflict,
                        "A csatolmány mentése {Attempts} kísérlet után sem sikerült | StorageKey: {StorageKey}",
                        maxRetries, dto.StorageKey);

                    throw new ValidationException("Nem sikerült menteni a fájlt, kérjük próbálja újra!");
                }
            }
            catch
            {
                //AZ IGÉNYLÉS VISSZAVONÁSA. Enélkül a megerősítés ténye megmaradna egy
                //csatolmány nélküli naplósoron: a takarító feladat ezért nem vinné el a
                //fájlt (megerősítettnek látja), a felhasználó viszont újra sem tudná
                //megerősíteni (az igénylés nem szabad) - a fájl örökre a tárolóban ragadna.
                //A visszavonással a helyzet ugyanaz, mint egy meg nem erősített feltöltésnél:
                //a takarító elviszi, és az újrapróbálkozás járható.
                await _context.PresignedUrlLogs
                    .Where(p => p.Id == log.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.Confirmed, false));

                throw;
            }

            //Innentől biztos, hogy a sor az adatbázisban van: a `saved` csak a visszatért
            //mentés után kap értéket, és a null esetet a fenti ág már kizárta.
            var attachment = saved;

            _logger.LogInformation("Fájl feltöltés megerősítve | StorageKey: {StorageKey} | FileName: {FileName}", attachment.StorageKey, attachment.FileName);

            //A feladat kulcsa és címe a válaszba ÉS az eseménybe is kell. A frissen létrehozott
            //soron a navigáció nincs bekötve, ezért külön olvassuk - ugyanúgy, ahogy a feltöltő
            //nevét. Enélkül a két payload eltérő teljességű lenne, és a kliens oldali
            //"ha már megvan, hagyd ki" gárda épp a SZEGÉNYEBBET tartaná meg.
            var task = attachment.TaskId.HasValue
                ? await _context.ProjectTasks.FirstOrDefaultAsync(t => t.Id == attachment.TaskId.Value)
                : null;

            try
            {
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    //A payload alakja AZONOS a REST válasz DTO-jával (AttachmentResponseDto),
                    //mezőnévre is. Korábban az `attachmentId` tért el az `Id`-tól, és a feladat
                    //kulcsa hiányzott - a kliens ezért nem tudta közvetlenül használni, hanem
                    //az esemény hatására újratöltötte a projekt csatolmányait ÉS a teljes
                    //feladatgráfot.
                    .SendAsync("AttachmentUploaded", new
                    {
                        id = attachment.Id,
                        projectId = attachment.ProjectId,
                        taskId = attachment.TaskId,
                        taskKey = task?.TaskKey,
                        taskTitle = task?.Title,
                        fileName = attachment.FileName,
                        contentType = attachment.ContentType,
                        sizeBytes = attachment.SizeBytes,
                        attachmentType = attachment.AttachmentType,
                        //A navigáció szándékosan nincs bekötve - a nevet a külön betöltött
                        //példányból adjuk, különben az esemény üres nevet hordozna.
                        uploadedByName = uploadedBy?.DisplayName ?? "",
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
                    $"{{actor}} feltöltötte a {log.FileName} fájlt"
                );
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("ActivityCreated", activity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: ActivityCreated | ProjectId: {ProjectId}", projectId);
            }

            //A nevet külön adjuk át: a navigációs tulajdonság ezen az úton szándékosan
            //nincs bekötve - lásd a megerősítés igénylésénél az indoklást.
            return MapToDto(attachment, uploadedBy?.DisplayName, task);
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

        /// <param name="attachment">A leképezendő csatolmány.</param>
        /// <param name="uploadedByName">
        /// A feltöltő neve, ha a hívó külön tudja. Azokon az utakon kell, ahol a navigációs
        /// tulajdonság szándékosan nincs bekötve - lásd a megerősítésnél az indoklást.
        /// </param>
        /// <param name="task">
        /// A feladat, ha a hívó külön betöltötte. Ugyanaz a minta, mint az
        /// <paramref name="uploadedByName"/>-nél: ahol a navigáció nincs bekötve (frissen
        /// létrehozott sor), ott a hívó adja - különben a kulcs némán üres lenne.
        /// </param>
        private AttachmentResponseDto MapToDto(
            Attachment attachment, string? uploadedByName = null, ProjectTask? task = null)
        {
            return new AttachmentResponseDto
            {
                Id = attachment.Id,
                ProjectId = attachment.ProjectId,
                TaskId = attachment.TaskId,
                TaskKey = task?.TaskKey ?? attachment.ProjectTask?.TaskKey,
                TaskTitle = task?.Title ?? attachment.ProjectTask?.Title,
                FileName = attachment.FileName,
                ContentType = attachment.ContentType,
                SizeBytes = attachment.SizeBytes,
                AttachmentType = attachment.AttachmentType,
                UploadedByName = uploadedByName ?? attachment.UploadedBy?.DisplayName ?? "Ismeretlen",
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
