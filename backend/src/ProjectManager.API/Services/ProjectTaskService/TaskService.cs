using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Common.Data;
using ProjectManager.API.Common.Exceptions;
using ProjectManager.API.Data;
using ProjectManager.API.DTOs.Attachment;
using ProjectManager.API.DTOs.Git;
using ProjectManager.API.DTOs.ProjectTask;
using ProjectManager.API.Hubs;
using ProjectManager.API.Model;
using ProjectManager.API.Services.ActivityService;
using ProjectManager.API.Services.CounterService;
using ProjectManager.API.Services.CurrentUserService;
using ProjectManager.API.Services.LexorankService;
using System.Data;

namespace ProjectManager.API.Services.ProjectTaskService
{
    public class TaskService : ITaskService
    {
        private readonly AppDbContext _context;
        private readonly ILexorankService _lexorankService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IHubContext<ProjectHub> _hubContext;
        private readonly IActivityService _activityService;
        private readonly ICounterService _counterService;
        private readonly ILogger<TaskService> _logger;

        public TaskService(
            AppDbContext context, 
            ILexorankService lexorankService, 
            ICurrentUserService currentUserService, 
            IHubContext<ProjectHub> hubContext, 
            IActivityService activityService,
            ICounterService counterService,
            ILogger<TaskService> logger)
        {
            _context = context;
            _lexorankService = lexorankService;
            _currentUserService = currentUserService;
            _hubContext = hubContext;
            _activityService = activityService;
            _counterService = counterService;
            _logger = logger;
        }
        
        public async Task<TaskResponseDto> CreateTaskAsync(Guid projectId, CreateTaskDto dto)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new NotFoundException("Projekt nem található");

            var createdById = _currentUserService.UserId;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == createdById);
            if (user == null)
                throw new NotFoundException("Felhasználó nem található!");

            //A DTO-ból érkező board- és oszlop-azonosító is a projekthez kell tartozzon,
            //különben idegen projekt boardjára lehetne taskot létrehozni
            if (dto.BoardId.HasValue)
            {
                var boardExists = await _context.Boards
                    .AnyAsync(b => b.Id == dto.BoardId && b.ProjectId == projectId);
                if (!boardExists)
                    throw new NotFoundException("Board nem található!");
            }

            ColumnDefinition? column = null;
            if (dto.ColumnId.HasValue)
            {
                column = await _context.ColumnDefinitions
                    .FirstOrDefaultAsync(cd => cd.Id == dto.ColumnId
                        && !cd.IsDeleted
                        && cd.Board.ProjectId == projectId);
                if (column == null)
                    throw new NotFoundException("Oszlop nem található!");
            }

            if (dto.SprintId.HasValue)
            {
                var sprintExists = await _context.Sprints
                    .AnyAsync(s => s.Id == dto.SprintId && s.ProjectId == projectId);
                if (!sprintExists)
                    throw new NotFoundException("Sprint nem található!");
            }

            var taskNumber = await _counterService.GetNextTaskNumberAsync(projectId);
            var taskKey = $"{project.ProjKey}-{taskNumber}";

            var lastTask = dto.ColumnId.HasValue
                ? await _context.ProjectTasks
                    .Where(t => t.ColumnId == dto.ColumnId)
                    .OrderBy(t => t.Position)
                    .LastOrDefaultAsync()
                : null;

            // CompletedAt beállítása ha az utolsó oszlopba van létrehozva
            DateTime? completedAt = null;
            if (dto.BoardId.HasValue && dto.ColumnId.HasValue)
            {
                var lastColumn = await _context.ColumnDefinitions
                    .Where(c => c.BoardId == dto.BoardId && c.Position > 0 && !c.IsDeleted)
                    .OrderByDescending(c => c.Position)
                    .FirstOrDefaultAsync();

                if (lastColumn?.Id == dto.ColumnId)
                    completedAt = DateTime.UtcNow;
            }

            var task = new ProjectTask
            {
                ProjectId = projectId,
                BoardId = dto.BoardId,
                ColumnId = dto.ColumnId,
                SprintId = dto.SprintId,
                CreatedById = createdById,
                TaskKey = taskKey,
                Title = dto.Title,
                Description = dto.Description,
                Priority = dto.Priority,
                Position = _lexorankService.GetInitialPosition(lastTask?.Position),
                EstimateInMinutes = dto.EstimateInMinutes ?? null,
                DueDate = dto.DueDate,
                CompletedAt = completedAt
            };
            await _context.ProjectTasks.AddAsync(task);
            _context.TaskStatusHistories.Add(new TaskStatusHistory
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                ColumnId = task.ColumnId,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            try
            {
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("TaskCreated", new
                    {
                        id = task.Id,
                        boardId = task.BoardId,
                        columnId = task.ColumnId,
                        sprintId = task.SprintId,
                        taskKey = task.TaskKey,
                        title = task.Title,
                        priority = task.Priority,
                        dueDate = task.DueDate,
                        estimateInMinutes = task.EstimateInMinutes,
                        position = task.Position,
                        createdAt = task.CreatedAt,
                        completedAt = task.CompletedAt,
                        rowVersion = task.xmin
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                    "TaskCreated", projectId);
            }

            try
            {
                var activity = await _activityService.LogActivityAsync(
                    projectId,
                    "Task",
                    task.Id,
                    "Created",
                    $"{{actor}} létrehozta a {task.TaskKey} taskot"
                );
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("ActivityCreated", activity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: ActivityCreated | ProjectId: {ProjectId}", projectId);
            }

            return MapToTaskResponseDto(
                task,
                new List<TaskAssignment>(),
                new List<LabelTask>(),
                new List<CommitLink>(),
                new List<PrLink>(),
                new List<Attachment>()
            );
        }

        public async Task DeleteTaskAsync(Guid projectId, Guid taskId)
        {
            //A jogosultság-ellenőrzés az URL projectId-jára vonatkozik, ezért a taskot
            //is arra kell szűrni - különben idegen projekt taskja is törölhető
            var task = await _context.ProjectTasks
                .FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId);
            if (task == null)
                throw new NotFoundException("Feladat nem található");

            // Cascade delete automatikusan törli a kapcsolódó entitásokat
            // (TaskAssignment, LabelTask, Comment, Attachment, Activity)
            // — konfigurálva: OnModelCreating Fluent API DeleteBehavior.Cascade
            
            _context.ProjectTasks.Remove(task);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ConflictException("A task időközben módosult, kérjük próbáld újra!");
            }

            try
            {
                await _hubContext.Clients
                    .Group($"project-{task.ProjectId}")
                    .SendAsync("TaskDeleted", new
                    {
                        taskId
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                    "TaskDeleted", task.ProjectId);
            }

            try
            {
                var activity = await _activityService.LogActivityAsync(
                    task.ProjectId,
                    "Task",
                    task.Id,
                    "Deleted",
                    $"{{actor}} törölte a {task.TaskKey} taskot"
                );
                await _hubContext.Clients
                    .Group($"project-{task.ProjectId}")
                    .SendAsync("ActivityCreated", activity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: ActivityCreated | ProjectId: {ProjectId}", task.ProjectId);
            }
        }

        public async Task<List<TaskResponseDto>> GetTasksAsync(
            Guid projectId,
            Guid? boardId = null,
            Guid? sprintId = null,
            string? scope = null)
        {
            var query = _context.ProjectTasks
                .Where(t => t.ProjectId == projectId)
                .Where(t => boardId == null || t.BoardId == boardId)
                .AsQueryable();

            if (scope == "initial")
            {
                // Backlog + Active + Planning sprintek taskjai
                // Optimálisabb: Valószinüleg nem kell alapvetően a Completed Sprintekhez tartozó taskok, ha mégis kell akkor külön le lehet kérni.
                query = query.Where(t =>
                    t.SprintId == null ||
                    t.Sprint!.State == "Active" ||
                    t.Sprint.State == "Planning");
            }
            else if (sprintId.HasValue)
            {
                query = query.Where(t => t.SprintId == sprintId);
            }

            var tasks = await query
                .Include(t => t.CreatedByUser)
                .Include(t => t.ColumnDefinition)
                .ToListAsync();

            //Magára a tasks listára query - Id- kinyerése
            var taskIds = tasks.Select(t => t.Id).ToList();

            //Id alapján az 5 listát feltöltjük
            var assignments = await _context.TaskAssignments
                .Where(ta => taskIds.Contains(ta.TaskId))
                .ToListAsync();

            var labels = await _context.LabelTasks
                .Where(lt => taskIds.Contains(lt.TaskId))
                .Include(lt => lt.Label)
                .ToListAsync();

            var commitLinks = await _context.CommitLinks
                .Where(cl => cl.TaskId.HasValue && taskIds.Contains(cl.TaskId.Value))
                .ToListAsync();

            var prLinks = await _context.PrLinks
                .Where(pl => pl.TaskId.HasValue && taskIds.Contains(pl.TaskId.Value))
                .ToListAsync();

            var attachments = await _context.Attachments
                .Where(a => a.TaskId.HasValue && taskIds.Contains(a.TaskId.Value))
                .Include(a => a.UploadedBy)
                .ToListAsync();

            return tasks.Select(t => MapToTaskResponseDto(
                t, assignments, labels, commitLinks, prLinks, attachments
            )).ToList();
        }

        public async Task<TaskResponseDto> GetTaskByIdAsync(Guid projectId, Guid taskId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new NotFoundException("Projekt nem található");

            var task = await _context.ProjectTasks
                .Include(t => t.CreatedByUser)
                .Include(t => t.ColumnDefinition)
                .FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId);
            if (task == null)
                throw new NotFoundException("Feladat nem található");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == task.CreatedById);
            if (user == null)
                throw new NotFoundException("Felhasználó nem található!");

            var assignments = await _context.TaskAssignments
                .Where(ta => ta.TaskId == task.Id)
                .ToListAsync();

            var labels = await _context.LabelTasks
                .Where(lt => lt.TaskId == task.Id)
                .Include(lt => lt.Label)
                .ToListAsync();

            var commitLinks = await _context.CommitLinks
                .Where(cl => cl.TaskId == task.Id)
                .ToListAsync();

            var prLinks = await _context.PrLinks
                .Where(pl => pl.TaskId == task.Id)
                .ToListAsync();

            var attachments = await _context.Attachments
                .Where(a => a.TaskId == task.Id)
                .Include(a => a.UploadedBy)
                .ToListAsync();

            return MapToTaskResponseDto(task, assignments, labels, commitLinks, prLinks, attachments);
        }

        public async Task<TaskResponseDto> MoveTaskAsync(Guid projectId, Guid taskId, MoveTaskDto dto)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new NotFoundException("Projekt nem található");

            var task = await _context.ProjectTasks
                .Include(t => t.CreatedByUser)
                .FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId);
            if (task == null)
                throw new NotFoundException("Feladat nem található");

            //A token ellenőrzése ÉS beállítása. Itt a sorrend többet jelent, mint a többi
            //helyen: ez a metódus a mentés előtt ÍRHAT a sorra (egy újrasorszámozás az egész
            //oszlopot átírja, benne ezt a feladatot is). Ellenőrzés nélkül tehát először
            //felülírnánk egy másik felhasználó módosítását, és csak utána kérdeznénk meg,
            //volt-e ilyen - amit akkor már nem is lehetne, mert a saját írásunk elmosta a
            //nyomát. Az itt megállapított tény az, ami feljogosít arra, hogy az
            //újrasorszámozás utáni új tokent alapvonalként átvegyük.
            _context.RequireRowVersion(task, dto.RowVersion,
                "A task időközben módosult, kérjük próbáld újra!");

            if (task.ClosedAt.HasValue)
                throw new ValidationException("Lezárt sprint taskja nem mozgatható!");

            ColumnDefinition? column = null;
            if (dto.ColumnId.HasValue)
            {
                column = await _context.ColumnDefinitions
                    .FirstOrDefaultAsync(cd => cd.Id == dto.ColumnId
                        && !cd.IsDeleted
                        && cd.Board.ProjectId == projectId);
                if (column == null)
                    throw new NotFoundException("Oszlop nem található");

                //A WIP limit ellenőrzése. VÁLLALT KORLÁT, hogy ez nem versenyhelyzet-biztos:
                //a darabszám leolvasása és a mozgatás mentése között egy másik felhasználó is
                //behúzhat egy feladatot, és a limit egyel túlléphető.
                //
                //Nem azért maradt így, mert nem tűnt fel. A rés megszüntetése ugyanazt az
                //eszközt igényelné, mint az oszloptörlésnél (sorosított tranzakció, mert az
                //állítás a sorok HALMAZÁRA szól) - csak itt a rendszer legforgalmasabb
                //műveletére, a DND mozgatásra kerülne rá, amit több felhasználó
                //másodpercenként többször is kiadhat, és ami már ma is több lekérdezést és
                //esetleg egy újrasorszámozást futtat.
                //
                //A csereüzlet azért rossz, mert a WIP limit TANÁCSADÓ szabály: a csapat
                //figyelmeztetésnek szánja, nem adatintegritási kényszernek. Egy eggyel
                //túllépett limit magától helyreáll (a következő mozgatás már elbukik), és
                //semmilyen adat nem lesz tőle elérhetetlen vagy ellentmondásos - szemben a
                //törölt oszlopban ragadó feladattal, amit ezért ott meg is védünk.
                if (column.WipLimit.HasValue)
                {
                    var currentTaskCount = await _context.ProjectTasks
                        .CountAsync(t => t.ColumnId == dto.ColumnId && t.Id != taskId);

                    if (currentTaskCount >= column.WipLimit.Value)
                        throw new ValidationException($"Az oszlop WIP limitje ({column.WipLimit.Value}) elérte a maximumot!");
                }
            }

            var neighbours = await FindNeighboursAsync(projectId, dto.ColumnId, dto.AfterTaskId, taskId);

            if (dto.AfterTaskId != null && neighbours.Prev == null)
                throw new NotFoundException("Előző feladat nem található");

            var prevTask = neighbours.Prev;
            var nextTask = neighbours.Next;

            // Duplikált sorrendkulcs esetén újrasorszámozás, majd újra lekérés
            if (dto.ColumnId.HasValue && neighbours.ColumnHasDuplicate)
            {
                var rebalancedRowVersion = await RebalanceColumnAsync(
                    dto.ColumnId.Value,
                    prevTask?.Position ?? nextTask!.Position,
                    taskId);

                //Az újrasorszámozás szerződése szerint innen semmi nem követett - a
                //feladatot újra be kell olvasni, különben az EF a mozgatást NEM írná ki.
                task = await ReloadTaskAfterRebalanceAsync(
                    projectId, taskId, dto.RowVersion, rebalancedRowVersion);

                neighbours = await FindNeighboursAsync(projectId, dto.ColumnId, dto.AfterTaskId, taskId);
                prevTask = neighbours.Prev;
                nextTask = neighbours.Next;
            }

            // Pozíció számítás – ha extrém edge case-ben kimerülne a hely, rebalance és újra
            string newPosition;
            try
            {
                newPosition = _lexorankService.GetMiddle(
                    prevTask?.Position,
                    nextTask?.Position
                );
            }
            catch (InvalidOperationException)
            {
                var rebalancedRowVersion = await RebalanceColumnAsync(
                    dto.ColumnId!.Value,
                    prevTask?.Position ?? nextTask!.Position,
                    taskId
                );

                task = await ReloadTaskAfterRebalanceAsync(
                    projectId, taskId, dto.RowVersion, rebalancedRowVersion);

                neighbours = await FindNeighboursAsync(projectId, dto.ColumnId, dto.AfterTaskId, taskId);
                prevTask = neighbours.Prev;
                nextTask = neighbours.Next;

                newPosition = _lexorankService.GetMiddle(prevTask?.Position, nextTask?.Position);
            }

            task.Position = newPosition;
            task.ColumnId = dto.ColumnId;
            task.BoardId = column?.BoardId;

            _context.TaskStatusHistories.Add(new TaskStatusHistory
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                ColumnId = task.ColumnId,
                CreatedAt = DateTime.UtcNow
            });

            bool isLastColumn = false;

            if (task.BoardId.HasValue)
            {
                var lastColumn = await _context.ColumnDefinitions
                    .Where(c => c.BoardId == task.BoardId && !c.IsDeleted)
                    .OrderByDescending(c => c.Position)
                    .FirstOrDefaultAsync();

                isLastColumn = lastColumn?.Id == task.ColumnId;

                if (lastColumn != null && task.ColumnId == lastColumn.Id)
                    task.CompletedAt = DateTime.UtcNow;
                else
                    task.CompletedAt = null;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ConflictException("A task időközben módosult, kérjük próbáld újra!");
            }

            try
            {
                await _hubContext.Clients
                    .Group($"project-{task.ProjectId}")
                    .SendAsync("TaskMoved", new
                    {
                        taskId = task.Id,
                        boardId = task.BoardId,
                        columnId = task.ColumnId,
                        sprintId = task.SprintId,
                        position = task.Position,
                        completedAt = task.CompletedAt,
                        triggeredBy = task.CreatedById,
                        status = column?.MapsToStatus ?? "Backlog",
                        rowVersion = task.xmin
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                    "TaskMoved", projectId);
            }

            if (isLastColumn)
            {
                try
                {
                    var activity = await _activityService.LogActivityAsync(
                        projectId,
                        "Task",
                        task.Id,
                        "Completed",
                        $"{{actor}} befejezte a {task.TaskKey} taskot"
                    );
                    await _hubContext.Clients
                        .Group($"project-{projectId}")
                        .SendAsync("ActivityCreated", activity);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SignalR broadcast hiba | Event: ActivityCreated | ProjectId: {ProjectId}", projectId);
                }
            }

            // NeedsRebalancing ellenőrzés a mentés után.
            //
            // Itt az újrasorszámozás KARBANTARTÁS, nem előfeltétel: a mozgatás már elmentve,
            // a pozíció érvényes, csak hosszú. Ha közben más is írja az oszlopot, az ütközés
            // miatt elhagyott újrasorszámozás egyetlen következménye, hogy a pozíciók
            // hosszúak maradnak a következő alkalomig - a felhasználó mozgatását viszont
            // nem buktathatja el. (A két ütközés miatti hívási hely ezzel szemben a művelet
            // előfeltétele, ott a kivétel szándékosan kiszáll.)
            if (dto.ColumnId.HasValue && _lexorankService.NeedsRebalancing(newPosition))
            {
                try
                {
                    await RebalanceColumnAsync(dto.ColumnId.Value, newPosition);
                }
                catch (ConflictException ex)
                {
                    _logger.LogWarning(ex,
                        "Az ujrasorszamozas utkozes miatt elmaradt, a poziciok hosszuak maradnak "
                        + "| ColumnId: {ColumnId} | {EventType}", dto.ColumnId.Value, "RebalanceDeferred");
                }
            }

            var assignments = await _context.TaskAssignments
                .Where(ta => ta.TaskId == task.Id)
                .ToListAsync();

            var labels = await _context.LabelTasks
                .Where(lt => lt.TaskId == task.Id)
                .Include(lt => lt.Label)
                .ToListAsync();

            var commitLinks = await _context.CommitLinks
                .Where(cl => cl.TaskId == task.Id)
                .ToListAsync();

            var prLinks = await _context.PrLinks
                .Where(pl => pl.TaskId == task.Id)
                .ToListAsync();

            var attachments = await _context.Attachments
                .Where(a => a.TaskId == task.Id)
                .Include(a => a.UploadedBy)
                .ToListAsync();

            return MapToTaskResponseDto(task, assignments, labels, commitLinks, prLinks, attachments);
        }

        public async Task<TaskResponseDto> UpdateTaskAsync(Guid projectId, Guid taskId, UpdateTaskDto dto)
        {
            var task = await _context.ProjectTasks
                .FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId);
            if (task == null)
                throw new NotFoundException("Feladat nem található");

            //A token ellenőrzése ÉS beállítása - lásd a ConcurrencyGuard leírását arról,
            //miért nem elég a mentés közbeni ellenőrzés.
            _context.RequireRowVersion(task, dto.RowVersion,
                "A task időközben módosult, kérjük próbáld újra!");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == task.CreatedById);
            if (user == null)
                throw new NotFoundException("Felhasználó nem található!");

            if (dto.Title != null) task.Title = dto.Title;
            if (dto.Description != null) task.Description = dto.Description;
            if (dto.BoardId != null) task.BoardId = dto.BoardId.Value;
            if (dto.SprintId != null) task.SprintId = dto.SprintId;
            if (dto.Priority != null) task.Priority = dto.Priority;
            if (dto.EstimateInMinutes.HasValue) task.EstimateInMinutes = dto.EstimateInMinutes.Value;
            if (dto.DueDate != null) task.DueDate = dto.DueDate;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ConflictException("A task időközben módosult, kérjük próbáld újra!");
            }

            try
            {
                await _hubContext.Clients
                    .Group($"project-{task.ProjectId}")
                    .SendAsync("TaskUpdated", new
                    {
                        taskId = task.Id,
                        title = task.Title,
                        description = task.Description,
                        priority = task.Priority,
                        dueDate = task.DueDate,
                        estimateInMinutes = task.EstimateInMinutes,
                        rowVersion = task.xmin
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                    "TaskUpdated", task.ProjectId);
            }

            try
            {
                var activity = await _activityService.LogActivityAsync(
                    task.ProjectId,
                    "Task",
                    task.Id,
                    "Updated",
                    $"{{actor}} módosította a {task.TaskKey} taskot"
                );
                await _hubContext.Clients
                    .Group($"project-{task.ProjectId}")
                    .SendAsync("ActivityCreated", activity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: ActivityCreated | ProjectId: {ProjectId}", task.ProjectId);
            }

            var assignments = await _context.TaskAssignments
                .Where(ta => ta.TaskId == task.Id)
                .ToListAsync();

            var labels = await _context.LabelTasks
                .Where(lt => lt.TaskId == task.Id)
                .Include(lt => lt.Label)
                .ToListAsync();

            var commitLinks = await _context.CommitLinks
                .Where(cl => cl.TaskId == task.Id)
                .ToListAsync();

            var prLinks = await _context.PrLinks
                .Where(pl => pl.TaskId == task.Id)
                .ToListAsync();

            var attachments = await _context.Attachments
                .Where(a => a.TaskId == task.Id)
                .Include(a => a.UploadedBy)
                .ToListAsync();

            return MapToTaskResponseDto(task, assignments, labels, commitLinks, prLinks, attachments);
        }

        public async Task<TaskResponseDto> AssignTaskToBoardAsync(Guid projectId, Guid taskId, AssignTaskToBoardDto dto)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new NotFoundException("Projekt nem található");

            var task = await _context.ProjectTasks
                .Include(t => t.CreatedByUser)
                .FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId);
            if (task == null)
                throw new NotFoundException("Task nem található");

            //A token ellenőrzése ÉS beállítása - lásd a ConcurrencyGuard leírását arról,
            //miért nem elég a mentés közbeni ellenőrzés.
            _context.RequireRowVersion(task, dto.RowVersion,
                "A task időközben módosult, kérjük próbáld újra!");

            if (!dto.BoardId.HasValue)
            {
                task.BoardId = null;
                task.ColumnId = null;
                task.Position = string.Empty;
                task.CompletedAt = null;

                //History a statisztikának
                _context.TaskStatusHistories.Add(new TaskStatusHistory
                {
                    Id = Guid.NewGuid(),
                    TaskId = task.Id,
                    ColumnId = null,
                    CreatedAt = DateTime.UtcNow
                });

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    throw new ConflictException("A task időközben módosult, kérjük próbáld újra!");
                }

                try
                {
                    await _hubContext.Clients
                        .Group($"project-{projectId}")
                        .SendAsync("TaskUpdated", new
                        {
                            taskId = task.Id,
                            boardId = (Guid?)null,
                            columnId = (Guid?)null,
                            position = task.Position,
                            rowVersion = task.xmin
                        });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                        "TaskUpdated", projectId);
                }
            }
            else
            {
                var board = await _context.Boards
                    .FirstOrDefaultAsync(b => b.Id == dto.BoardId && b.ProjectId == projectId);
                if (board == null)
                    throw new NotFoundException("Board nem található");

                var backlogColumn = await _context.ColumnDefinitions
                    .FirstOrDefaultAsync(c => c.Position == 0 && c.BoardId == dto.BoardId);
                if (backlogColumn == null)
                    throw new NotFoundException("Backlog oszlop nem található");
                
                task.BoardId = dto.BoardId;
                task.CompletedAt = null;
                if (dto.BoardId.HasValue && task.SprintId.HasValue)
                {
                    var sprint = await _context.Sprints
                        .FirstOrDefaultAsync(s => s.Id == task.SprintId);

                    var firstColumn = await _context.ColumnDefinitions
                            .Where(c => c.BoardId == dto.BoardId && c.Position > 0 && !c.IsDeleted)
                            .OrderBy(c => c.Position)
                            .FirstOrDefaultAsync();
                    
                    if (sprint?.State == "Active" && firstColumn != null)
                    {
                        var lastTask = await _context.ProjectTasks
                                .Where(t => t.ColumnId == firstColumn.Id)
                                .OrderBy(t => t.Position)
                                .LastOrDefaultAsync();

                        task.ColumnId = firstColumn.Id;
                        task.Position = _lexorankService.GetInitialPosition(lastTask?.Position);

                        //History a statisztikának
                        _context.TaskStatusHistories.Add(new TaskStatusHistory
                        {
                            Id = Guid.NewGuid(),
                            TaskId = task.Id,
                            ColumnId = firstColumn.Id,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        // Nem aktív sprint vagy nincs első oszlop - Board Backlog oszlopba
                        var lastTask = await _context.ProjectTasks
                            .Where(t => t.ColumnId == backlogColumn.Id)
                            .OrderBy(t => t.Position)
                            .LastOrDefaultAsync();
                        
                        task.ColumnId = backlogColumn.Id;
                        task.Position = _lexorankService.GetInitialPosition(lastTask?.Position);

                        _context.TaskStatusHistories.Add(new TaskStatusHistory
                        {
                            Id = Guid.NewGuid(),
                            TaskId = task.Id,
                            ColumnId = backlogColumn.Id,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
                else
                {
                    // Lexorank pozíció az oszlop végére helyezés
                    var lastTask = await _context.ProjectTasks
                        .Where(t => t.ColumnId == backlogColumn.Id)
                        .OrderBy(t => t.Position)
                        .LastOrDefaultAsync();

                    // Nincs sprint - Board Backlog oszlopba
                    task.ColumnId = backlogColumn.Id;
                    task.Position = _lexorankService.GetInitialPosition(lastTask?.Position);
                }

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    throw new ConflictException("A task időközben módosult, kérjük próbáld újra!");
                }

                try
                {
                    await _hubContext.Clients
                        .Group($"project-{projectId}")
                        .SendAsync("TaskUpdated", new
                        {
                            taskId = task.Id,
                            boardId = task.BoardId,
                            columnId = task.ColumnId,
                            position = task.Position,
                            rowVersion = task.xmin
                        });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                        "TaskUpdated", projectId);
                }

                try
                {
                    var activity = await _activityService.LogActivityAsync(
                        task.ProjectId,
                        "Task",
                        task.Id,
                        "BoardAssigned",
                        $"{{actor}} boardhoz rendelte a {task.TaskKey} taskot"
                    );
                    await _hubContext.Clients
                        .Group($"project-{task.ProjectId}")
                        .SendAsync("ActivityCreated", activity);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SignalR broadcast hiba | Event: ActivityCreated | ProjectId: {ProjectId}", projectId);
                }
            }

            // Response összerakása
            var assignments = await _context.TaskAssignments
                .Where(ta => ta.TaskId == task.Id)
                .ToListAsync();

            var labels = await _context.LabelTasks
                .Where(lt => lt.TaskId == task.Id)
                .Include(lt => lt.Label)
                .ToListAsync();

            var commitLinks = await _context.CommitLinks
                .Where(cl => cl.TaskId == task.Id)
                .ToListAsync();

            var prLinks = await _context.PrLinks
                .Where(pl => pl.TaskId == task.Id)
                .ToListAsync();

            var attachments = await _context.Attachments
                .Where(a => a.TaskId == task.Id)
                .Include(a => a.UploadedBy)
                .ToListAsync();

            return MapToTaskResponseDto(task, assignments, labels, commitLinks, prLinks, attachments);
        }

        // A mozgatás referenciája és a mögötte következő feladat, plusz az a jelzés, hogy az
        // oszlopban van-e duplikált sorrendkulcs a beszúrás helyén.
        private sealed record NeighbourLookup(
            ProjectTask? Prev, ProjectTask? Next, bool ColumnHasDuplicate);

        /// <summary>
        /// A beszúrás két szomszédjának megkeresése, a duplikált kulcs felismerésével.
        /// </summary>
        /// <remarks>
        /// <b>Miért van ez egy helyen?</b> Ugyanez a lekérdezés háromszor szerepelt a
        /// mozgatásban (az első keresés és két újrakeresés újrasorszámozás után), és a három
        /// példány között volt egy apró eltérés, amely egy teljes védelmi ágat tett halottá -
        /// lásd alább. Egy ismételt lekérdezés, amelynek a szemantikája számít, pontosan az,
        /// amit egy helyen kell kimondani.
        ///
        /// <b>A javított hiba.</b> A szomszédot korábban szigorúan nagyobb pozícióra szűrtük
        /// (<c>&gt; 0</c>), miközben az ütközés-vizsgálat egyenlőséget keres
        /// (<c>HasCollision(a, b) =&gt; a == b</c>). A két feltétel kizárta egymást: a
        /// megtalált szomszéd pozíciója sosem lehetett egyenlő a horgonnyal, tehát az
        /// újrasorszámozást kiváltó ág SOHA nem futott le. A szűrő mostantól
        /// <c>&gt;= 0</c>, és a referenciát magát zárja ki - így a referencia ikerpárja
        /// megtalálható, és a duplikáció felismerhető.
        ///
        /// <b>Amit ez megold, és amit nem.</b> Ez JAVÍTÁS, nem megelőzés. Két egyidejű,
        /// ugyanabba a résbe irányuló mozgatás továbbra is létrehozhat két azonos kulcsú
        /// feladatot: a számítás pillanatában még egyik kérés sem lát duplikációt. Amit
        /// megad, az az, hogy a duplikáció nem él tovább korlátlanul - a következő, a
        /// duplikált kulcsra referáló beszúrás kitakarítja az oszlopot.
        ///
        /// <b>Az oszlop elejére szúrás külön eset.</b> Ott nincs referencia, amihez hasonlítani
        /// lehetne, ezért a lekérdezés az első KETTŐ feladatot hozza, és a duplikációt azok
        /// egyenlősége jelzi. Enélkül az oszlop elején lévő ikerpár soha nem javulna, mert az
        /// elejére szúrás mindig mindkettő ELÉ kerül - tehát sosem keletkezne jelzés. Ugyanaz
        /// az invariáns ("egy oszlopon belül a kulcsok egyediek"), két felismerési móddal.
        ///
        /// <b>Miért nem drágább ez a gyakori úton.</b> A lekérdezés darabszáma nem változik,
        /// és egyedi kulcsok mellett a <c>&gt;=</c> ugyanazt a sort adja, mint a <c>&gt;</c> -
        /// a referencia ki van zárva, rajta kívül pedig nincs vele egyenlő. Tehát a mozgatás
        /// továbbra is EGY sort ír; az N soros újrasorszámozás ugyanúgy kivételes marad, mint
        /// a hossztúllépésnél.
        /// </remarks>
        private async Task<NeighbourLookup> FindNeighboursAsync(
            Guid projectId, Guid? columnId, Guid? afterTaskId, Guid movedTaskId)
        {
            ProjectTask? prev = null;
            if (afterTaskId != null)
            {
                prev = await _context.ProjectTasks
                    .FirstOrDefaultAsync(t => t.Id == afterTaskId && t.ProjectId == projectId);
            }

            if (prev == null)
            {
                //Az oszlop elejére: az első kettő kell, hogy az ikerpár látszódjon.
                var firstTwo = await _context.ProjectTasks
                    .Where(t => t.ProjectId == projectId
                             && t.ColumnId == columnId
                             && t.Id != movedTaskId)
                    .OrderBy(t => t.Position)
                    .Take(2)
                    .ToListAsync();

                var hasDuplicateAtFront = firstTwo.Count == 2
                    && _lexorankService.HasCollision(firstTwo[0].Position, firstTwo[1].Position);

                return new NeighbourLookup(null, firstTwo.FirstOrDefault(), hasDuplicateAtFront);
            }

            var next = await _context.ProjectTasks
                .Where(t => t.ProjectId == projectId
                         && t.ColumnId == columnId
                         && string.Compare(t.Position, prev.Position) >= 0
                         && t.Id != movedTaskId
                         && t.Id != prev.Id)
                .OrderBy(t => t.Position)
                .FirstOrDefaultAsync();

            //A referencia oszlopához kötve: a kérés átadhat egy MÁS oszlopban lévő feladatot
            //referenciaként (a lekérdezése nem szűr oszlopra), és olyankor a cél oszlop egy
            //véletlenül egyező pozíciója téves újrasorszámozást váltana ki. A duplikáció
            //állítása csak akkor értelmes, ha a referencia tényleg ebben az oszlopban van.
            var hasDuplicate = next != null
                && prev.ColumnId == columnId
                && _lexorankService.HasCollision(prev.Position, next.Position);

            return new NeighbourLookup(prev, next, hasDuplicate);
        }

        // A mozgatott feladat újraolvasása és a konkurencia-alapvonal helyreállítása egy
        // újrasorszámozás után.
        private async Task<ProjectTask> ReloadTaskAfterRebalanceAsync(
            Guid projectId, Guid taskId, uint clientRowVersion, uint? rebalancedRowVersion)
        {
            var task = await _context.ProjectTasks
                .Include(t => t.CreatedByUser)
                .FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId);

            //Az újrasorszámozás tranzakciója alatt más törölhette a feladatot.
            if (task == null)
                throw new NotFoundException("Feladat nem található");

            _context.Entry(task).OriginalValues["xmin"] = rebalancedRowVersion ?? clientRowVersion;
            return task;
        }

        /// <summary>
        /// Egy oszlop összes feladatának újrasorszámozása a következő bucketben.
        /// </summary>
        /// <remarks>
        /// Sorosított tranzakcióban fut, mert az állítás a sorok HALMAZÁRA szól ("a pozíciók
        /// szigorúan növekvők"), amit az xmin konkurencia-token nem tud kikényszeríteni: az
        /// egyenként nézi a sorokat.
        ///
        /// Újrapróbálás azért van, és a számlálónál is azért van, mert ez a művelet
        /// AUTOMATIKUSAN indul, a felhasználó akarata nélkül - egy ütközés esetén tehát nem
        /// tudja, mit kellene újrapróbálnia. A hívási helyek közül kettő (ütközés miatti
        /// újrasorszámozás) a művelet ELŐFELTÉTELE, ott a hiba nem elnyelhető; a harmadik
        /// (hossztúllépés utáni karbantartás) elhalasztható - lásd ott.
        /// </remarks>
        /// <param name="columnId">Az újrasorszámozandó oszlop azonosítója.</param>
        /// <param name="position">
        /// Egy pozíció az oszlop JELENLEGI bucketjéből; ebből adódik a következő bucket.
        /// </param>
        /// <param name="trackedTaskId">
        /// Annak a feladatnak az azonosítója, amelyet a HÍVÓ ment el ezután. Ha az
        /// újrasorszámozás ezt a sort is átírta, a metódus a <b>maga által írt</b> új
        /// konkurencia-tokent adja vissza.
        /// </param>
        /// <returns>
        /// A <paramref name="trackedTaskId"/> feladat új tokenje, ha az újrasorszámozás
        /// érintette; egyébként <c>null</c>.
        ///
        /// Ez azért kell, mert az újrasorszámozás a RENDSZER saját írása, nem emberi
        /// ütközés. A hívó alapvonalát ezért nem lehet sem meghagyni (akkor a mentés 409-cel
        /// bukna egy teljesen jogos mozgatásnál), sem a friss adatbázis-értékre állítani
        /// (akkor egy közben beérkező IDEGEN írás is némán elfogadódna). A helyes érték az,
        /// amit mi magunk írtunk - azt adja vissza ez a metódus.
        ///
        /// <b>A metódus szerződése a változáskövetőre nézve:</b> visszatérés után
        /// <b>semmi nem követett</b>. A hívónak újra be kell olvasnia, amivel dolgozni akar.
        /// Ez szándékosan egységes a sikeres és a bukott ágon. Korábban a kettő eltért, és
        /// ez két néma hibát okozott: újrapróbálásnál a <c>ChangeTracker.Clear()</c> a hívó
        /// feladatát is leválasztotta (az EF ezután nem írta ki a mozgatást, a válasz mégis
        /// 200 volt), siker esetén pedig a mentés visszaállította a token alapvonalát, tehát
        /// a mozgatás ütközés-ellenőrzés nélkül maradt. Egy kiszámíthatatlan mellékhatás
        /// helyett így van egy kimondott szabály.
        /// </returns>
        private async Task<uint?> RebalanceColumnAsync(Guid columnId, string position, Guid? trackedTaskId = null)
        {
            const int maxAttempts = 3;

            try
            {
                for (var attempt = 0; attempt < maxAttempts; attempt++)
                {
                    try
                    {
                        return await RebalanceColumnOnceAsync(columnId, position, trackedTaskId);
                    }
                    catch (Exception ex) when (ConcurrencyErrors.IsConflict(ex) && attempt < maxAttempts - 1)
                    {
                        //A bukott kísérlet entitásállapotát el kell dobni, különben a következő
                        //próbálkozás a már módosított, nem mentett pozíciókkal indulna.
                        _context.ChangeTracker.Clear();
                        await Task.Delay(TimeSpan.FromMilliseconds(50 * (attempt + 1)));
                    }
                    catch (Exception ex) when (ConcurrencyErrors.IsConflict(ex))
                    {
                        throw new ConflictException(
                            "A feladatok sorrendje időközben módosult. Töltsd újra az oldalt, és próbáld újra!");
                    }
                }

                return null;
            }
            finally
            {
                //A hívó sosem kaphat félig követett állapotot vissza.
                //A `finally` azért kell, hogy a kiszálló ágon (ConflictException) se maradjon
                //bent a részlegesen módosított oszlop.
                _context.ChangeTracker.Clear();
            }
        }

        private async Task<uint?> RebalanceColumnOnceAsync(Guid columnId, string position, Guid? trackedTaskId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var column = await _context.ColumnDefinitions
                    .Include(c => c.Board)
                    .FirstOrDefaultAsync(c => c.Id == columnId && !c.IsDeleted);

                var bucket = _lexorankService.GetBucket(position);
                var nextBucket = _lexorankService.GetNextBucket(bucket);

                var allTasksInColumn = await _context.ProjectTasks
                    .Where(t => t.ColumnId == columnId)
                    .OrderBy(t => t.Position)
                    .ToListAsync();

                if (allTasksInColumn.Count == 0)
                {
                    await transaction.RollbackAsync();
                    return null;
                }

                var newPositions = _lexorankService.RebalancePositions(
                    allTasksInColumn.Count,
                    nextBucket
                );

                for (int i = 0; i < allTasksInColumn.Count; i++)
                {
                    allTasksInColumn[i].Position = newPositions[i];
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                //A hívó feladatának új tokenje, amit MI írtunk. A mentés után az EF
                //visszaolvasta a sor friss xmin-jét, tehát ez az az érték, amely a
                //hívó mentésének WHERE záradékába kerülhet - lásd a metódus leírását.
                var trackedTaskRowVersion = trackedTaskId.HasValue
                    ? allTasksInColumn.FirstOrDefault(t => t.Id == trackedTaskId.Value)?.xmin
                    : null;

                try
                {
                    await _hubContext.Clients
                        .Group($"project-{column!.Board.ProjectId}")
                        .SendAsync("TasksRebalanced", new
                        {
                            boardId = column.BoardId,
                            columnId,
                            tasks = allTasksInColumn.Select(t => new {
                                id = t.Id,
                                position = t.Position,
                                rowVersion = t.xmin
                            })
                        });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                        "TasksRebalanced", column!.Board.ProjectId);
                }

                return trackedTaskRowVersion;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task AddAssigneeAsync(Guid projectId, Guid taskId, Guid userId)
        {
            var task = await _context.ProjectTasks
                .FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId);
            if (task == null)
                throw new NotFoundException("Task nem található!");

            //Csak a projekt tagja rendelhető hozzá: enélkül tetszőleges felhasználó
            //azonosítója beküldhető, és a válasz visszaadja a nevét
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == userId
                    && _context.ProjectMembers.Any(pm => pm.ProjectId == projectId && pm.UserId == u.Id));
            if (user == null)
                throw new NotFoundException("Felhasználó nem található!");

            var existing = await _context.TaskAssignments
                .FirstOrDefaultAsync(ta => ta.TaskId == taskId && ta.UserId == userId);
            if (existing != null)
                throw new ConflictException("Ez a felhasználó már hozzá van rendelve!");

            await _context.TaskAssignments.AddAsync(new TaskAssignment
            {
                TaskId = taskId,
                UserId = userId
            });

            await _context.SaveChangesAsync();

            try
            {
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("TaskAssigneeAdded", new { taskId, userId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                    "TaskAssigneeAdded", projectId);
            }
            
            try
            {
                var activity = await _activityService.LogActivityAsync(
                    projectId,
                    "Task",
                    taskId,
                    "AssigneeAdded",
                    $"{{actor}} hozzárendelte {{target}}-t a {task.TaskKey} taskhoz",
                    targetUserId: user.Id
                );
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("ActivityCreated", activity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: ActivityCreated | ProjectId: {ProjectId}", projectId);
            }
        }

        public async Task RemoveAssigneeAsync(Guid projectId, Guid taskId, Guid userId)
        {
            //A projekt-szűrés előbb fut, mint a hozzárendelés keresése: így idegen
            //projekt taskjáról nem derül ki, hogy egyáltalán létezik-e
            var task = await _context.ProjectTasks
                .FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId);
            if (task == null)
                throw new NotFoundException("Task nem található!");

            var assignment = await _context.TaskAssignments
                .FirstOrDefaultAsync(ta => ta.TaskId == taskId && ta.UserId == userId);
            if (assignment == null)
                throw new ValidationException("Ez a felhasználó nincs hozzárendelve!");

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                throw new NotFoundException("Felhasználó nem található!");


            _context.TaskAssignments.Remove(assignment);
            await _context.SaveChangesAsync();

            try
            {
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("TaskAssigneeRemoved", new { taskId, userId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                    "TaskAssigneeRemoved", projectId);
            }

            try
            {
                var activity = await _activityService.LogActivityAsync(
                    projectId,
                    "Task",
                    taskId,
                    "AssigneeRemoved",
                    $"{{actor}} eltávolította {{target}}-t a {task.TaskKey} taskból",
                    targetUserId: user.Id
                );
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("ActivityCreated", activity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: ActivityCreated | ProjectId: {ProjectId}", projectId);
            }
        }

        private TaskResponseDto MapToTaskResponseDto(
            ProjectTask t,
            List<TaskAssignment> assignments,
            List<LabelTask> labels,
            List<CommitLink> commitLinks,
            List<PrLink> prLinks,
            List<Attachment> attachments)
        {
            return new TaskResponseDto
            {
                Id = t.Id,
                ProjectId = t.ProjectId,
                BoardId = t.BoardId,
                ColumnId = t.ColumnId,
                SprintId = t.SprintId,
                AssigneeIds = assignments
                    .Where(ta => ta.TaskId == t.Id)
                    .Select(ta => ta.UserId.ToString())
                    .ToList(),
                LabelIds = labels
                    .Where(lt => lt.TaskId == t.Id)
                    .Select(lt => lt.LabelId.ToString())
                    .ToList(),
                CommitLinks = commitLinks
                    .Where(cl => cl.TaskId == t.Id)
                    .Select(cl => new CommitLinkResponseDto
                    {
                        Id = cl.Id,
                        CommitSha = cl.CommitSha,
                        CommitUrl = cl.CommitUrl,
                        Message = cl.Message,
                        AuthorName = cl.AuthorName,
                        CommittedAt = cl.CommittedAt
                    })
                    .ToList(),
                PrLinks = prLinks
                    .Where(pl => pl.TaskId == t.Id)
                    .Select(pl => new PrLinkResponseDto
                    {
                        Id = pl.Id,
                        PrNumber = pl.PrNumber,
                        PrUrl = pl.PrUrl,
                        Title = pl.Title,
                        State = pl.State,
                        AuthorName = pl.AuthorName,
                        CreatedAt = pl.CreatedAt,
                        MergedAt = pl.MergedAt
                    })
                    .ToList(),
                Attachments = attachments
                    .Where(a => a.TaskId == t.Id)
                    .Select(a => new AttachmentResponseDto
                    {
                        Id = a.Id,
                        ProjectId = a.ProjectId,
                        TaskId = a.TaskId,
                        FileName = a.FileName,
                        ContentType = a.ContentType,
                        SizeBytes = a.SizeBytes,
                        AttachmentType = a.AttachmentType,
                        UploadedByName = a.UploadedBy?.DisplayName ?? "Ismeretlen",
                        CreatedAt = a.CreatedAt
                    })
                    .ToList(),
                CreatedByName = t.CreatedByUser?.DisplayName ?? "Ismeretlen",
                TaskKey = t.TaskKey,
                Title = t.Title,
                Description = t.Description,
                Status = t.ColumnDefinition?.MapsToStatus ?? "Backlog",
                Priority = t.Priority,
                Position = t.Position,
                EstimateInMinutes = t.EstimateInMinutes,
                RowVersion = t.xmin,
                DueDate = t.DueDate,
                ClosedAt = t.ClosedAt,
                CompletedAt = t.CompletedAt,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            };
        }
    }
}
