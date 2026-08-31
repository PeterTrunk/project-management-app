using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Pipelines.Sockets.Unofficial.Arenas;
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
                throw new Exception("Projekt nem található");

            var createdById = _currentUserService.UserId;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == createdById);
            if (user == null)
                throw new Exception("Felhasználó nem található!");

            ColumnDefinition? column = null;
            if (dto.ColumnId.HasValue)
            {
                column = await _context.ColumnDefinitions
                    .FirstOrDefaultAsync(cd => cd.Id == dto.ColumnId && !cd.IsDeleted);
                if (column == null)
                    throw new Exception("Oszlop nem található!");
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
                    $"{_currentUserService.DisplayName} létrehozta a {task.TaskKey} taskot"
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

        public async Task DeleteTaskAsync(Guid taskId)
        {
            var task = await _context.ProjectTasks.FirstOrDefaultAsync(t => t.Id == taskId);
            if (task == null)
                throw new Exception("Feladat nem található");

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
                throw new Exception("A task időközben módosult, kérjük próbáld újra!");
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
                    $"{_currentUserService.DisplayName} törölte a {task.TaskKey} taskot"
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
                    t.Sprint.State == "Active" ||
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
                throw new Exception("Projekt nem található");

            var task = await _context.ProjectTasks
                .Include(t => t.CreatedByUser)
                .Include(t => t.ColumnDefinition)
                .FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId);
            if (task == null)
                throw new Exception("Feladat nem található");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == task.CreatedById);
            if (user == null)
                throw new Exception("Felhasználó nem található!");

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
                throw new Exception("Projekt nem található");

            var task = await _context.ProjectTasks
                .Include(t => t.CreatedByUser)
                .FirstOrDefaultAsync(t => t.Id == taskId);
            if (task == null)
                throw new Exception("Feladat nem található");

            //RowVersion beállítása az optimistic concurrency-hez
            _context.Entry(task).OriginalValues["xmin"] = dto.RowVersion;

            if (task.ClosedAt.HasValue)
                throw new Exception("Lezárt sprint taskja nem mozgatható!");

            ColumnDefinition? column = null;
            if (dto.ColumnId.HasValue)
            {
                column = await _context.ColumnDefinitions
                    .FirstOrDefaultAsync(cd => cd.Id == dto.ColumnId && !cd.IsDeleted);
                if (column == null)
                    throw new Exception("Oszlop nem található");
            }

            ProjectTask? prevTask = null;
            if (dto.AfterTaskId != null)
            {
                prevTask = await _context.ProjectTasks
                    .FirstOrDefaultAsync(t => t.Id == dto.AfterTaskId);
                if (prevTask == null)
                    throw new Exception("Előző feladat nem található");
            }

            ProjectTask? nextTask;
            if (prevTask == null)
            {
                nextTask = await _context.ProjectTasks
                    .Where(t => t.ColumnId == dto.ColumnId && t.Id != taskId)
                    .OrderBy(t => t.Position)
                    .FirstOrDefaultAsync();
            }
            else
            {
                nextTask = await _context.ProjectTasks
                    .Where(t => t.ColumnId == dto.ColumnId
                             && string.Compare(t.Position, prevTask.Position) > 0
                             && t.Id != taskId)
                    .OrderBy(t => t.Position)
                    .FirstOrDefaultAsync();
            }

            // Ütközés esetén rebalance, majd újra lekérés
            if (dto.ColumnId.HasValue &&
                prevTask != null &&
                nextTask != null &&
                _lexorankService.HasCollision(prevTask.Position, nextTask.Position))
            {
                await RebalanceColumnAsync(dto.ColumnId.Value, prevTask.Position);

                prevTask = dto.AfterTaskId != null
                    ? await _context.ProjectTasks.FirstOrDefaultAsync(t => t.Id == dto.AfterTaskId)
                    : null;

                nextTask = prevTask == null
                    ? await _context.ProjectTasks
                        .Where(t => t.ColumnId == dto.ColumnId && t.Id != taskId)
                        .OrderBy(t => t.Position)
                        .FirstOrDefaultAsync()
                    : await _context.ProjectTasks
                        .Where(t => t.ColumnId == dto.ColumnId
                                 && string.Compare(t.Position, prevTask.Position) > 0
                                 && t.Id != taskId)
                        .OrderBy(t => t.Position)
                        .FirstOrDefaultAsync();
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
                await RebalanceColumnAsync(
                    dto.ColumnId!.Value,
                    prevTask?.Position ?? nextTask!.Position
                );

                prevTask = dto.AfterTaskId != null
                    ? await _context.ProjectTasks.FirstOrDefaultAsync(t => t.Id == dto.AfterTaskId)
                    : null;

                nextTask = prevTask == null
                    ? await _context.ProjectTasks
                        .Where(t => t.ColumnId == dto.ColumnId && t.Id != taskId)
                        .OrderBy(t => t.Position)
                        .FirstOrDefaultAsync()
                    : await _context.ProjectTasks
                        .Where(t => t.ColumnId == dto.ColumnId
                                 && string.Compare(t.Position, prevTask.Position) > 0
                                 && t.Id != taskId)
                        .OrderBy(t => t.Position)
                        .FirstOrDefaultAsync();

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
                throw new Exception("A task időközben módosult, kérjük próbáld újra!");
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
                        $"{_currentUserService.DisplayName} befejezte a {task.TaskKey} taskot"
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

            // NeedsRebalancing ellenőrzés a mentés után
            if (dto.ColumnId.HasValue && _lexorankService.NeedsRebalancing(newPosition))
            {
                await RebalanceColumnAsync(dto.ColumnId.Value, newPosition);
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

        public async Task<TaskResponseDto> UpdateTaskAsync(Guid taskId, UpdateTaskDto dto)
        {
            var task = await _context.ProjectTasks.FirstOrDefaultAsync(t => t.Id == taskId);
            if (task == null)
                throw new Exception("Feladat nem található");

            //RowVersion beállítása az optimistic concurrency-hez
            _context.Entry(task).OriginalValues["xmin"] = dto.RowVersion;

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == task.CreatedById);
            if (user == null)
                throw new Exception("Felhasználó nem található!");

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
                throw new Exception("A task időközben módosult, kérjük próbáld újra!");
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
                    $"{_currentUserService.DisplayName} módosította a {task.TaskKey} taskot"
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
                throw new Exception("Projekt nem található");

            var task = await _context.ProjectTasks
                .Include(t => t.CreatedByUser)
                .FirstOrDefaultAsync(t => t.Id == taskId);
            if (task == null)
                throw new Exception("Task nem található");

            //RowVersion beállítása az optimistic concurrency-hez
            _context.Entry(task).OriginalValues["xmin"] = dto.RowVersion;

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
                    throw new Exception("A task időközben módosult, kérjük próbáld újra!");
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
                var board = await _context.Boards.FirstOrDefaultAsync(b => b.Id == dto.BoardId);
                if (board == null)
                    throw new Exception("Board nem található");

                var backlogColumn = await _context.ColumnDefinitions
                    .FirstOrDefaultAsync(c => c.Position == 0 && c.BoardId == dto.BoardId);
                if (backlogColumn == null)
                    throw new Exception("Backlog oszlop nem található");
                
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
                    throw new Exception("A task időközben módosult, kérjük próbáld újra!");
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
                        $"{_currentUserService.DisplayName} boardhoz rendelte a {task.TaskKey} taskot"
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

        private async Task RebalanceColumnAsync(Guid columnId, string position)
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
                    return;
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
                throw new Exception("Task nem található!");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                throw new Exception("Felhasználó nem található!");

            var existing = await _context.TaskAssignments
                .FirstOrDefaultAsync(ta => ta.TaskId == taskId && ta.UserId == userId);
            if (existing != null)
                throw new Exception("Ez a felhasználó már hozzá van rendelve!");

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
                    $"{_currentUserService.DisplayName} hozzárendelte {user.DisplayName}-t a {task.TaskKey} taskhoz"
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
            var assignment = await _context.TaskAssignments
                .FirstOrDefaultAsync(ta => ta.TaskId == taskId && ta.UserId == userId);
            if (assignment == null)
                throw new Exception("Ez a felhasználó nincs hozzárendelve!");

            var task = await _context.ProjectTasks
                .FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId);
            if (task == null)
                throw new Exception("Task nem található!");

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                throw new Exception("Felhasználó nem található!");


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
                    $"{_currentUserService.DisplayName} eltávolította {user.DisplayName}-t a {task.TaskKey} taskból"
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
                        AuthorEmail = cl.AuthorEmail,
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
