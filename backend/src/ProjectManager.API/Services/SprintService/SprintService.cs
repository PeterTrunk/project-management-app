using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Data;
using ProjectManager.API.DTOs.Attachment;
using ProjectManager.API.DTOs.Git;
using ProjectManager.API.DTOs.ProjectTask;
using ProjectManager.API.DTOs.Sprints;
using ProjectManager.API.Hubs;
using ProjectManager.API.Model;
using ProjectManager.API.Services.ActivityService;
using ProjectManager.API.Services.CurrentUserService;
using ProjectManager.API.Services.LexorankService;
using System.Data;
using ProjectManager.API.Common.Constants;

namespace ProjectManager.API.Services.SprintService
{
    public class SprintService : ISprintService
    {
        private readonly AppDbContext _context;
        private readonly ILexorankService _lexorankService;
        private readonly IHubContext<ProjectHub> _hubContext;
        private readonly ICurrentUserService _currentUserService;
        private readonly IActivityService _activityService;
        private readonly ILogger<SprintService> _logger;
        
        public SprintService(
            AppDbContext context, 
            ILexorankService lexorankService, 
            IHubContext<ProjectHub> hubContext, 
            ICurrentUserService currentUserService, 
            IActivityService activityService,
            ILogger<SprintService> logger)
        {
            _context = context;
            _lexorankService = lexorankService;
            _hubContext = hubContext;
            _currentUserService = currentUserService;
            _activityService = activityService;
            _logger = logger;
        }

        public async Task<SprintResponseDto> ActivateSprintAsync(Guid projectId, Guid sprintId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található");

            var sprint = await _context.Sprints.FirstOrDefaultAsync(s => s.Id == sprintId);
            if (sprint == null)
                throw new Exception("Sprint nem található");

            using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                // Tranzakción belül ellenőrzünk - csak az adott projekt aktív sprintjét nézi!
                var currentlyActive = await _context.Sprints
                    .AnyAsync(s => s.State == SprintStates.Active && s.ProjectId == projectId);
                if (currentlyActive)
                    throw new Exception("Már van Aktív sprint!");

                sprint.State = SprintStates.Active;

                var sprintTasks = await _context.ProjectTasks
                    .Where(t => t.SprintId == sprintId)
                    .ToListAsync();

                foreach (var task in sprintTasks)
                {
                    if (task.BoardId.HasValue)
                    {
                        var firstColumn = await _context.ColumnDefinitions
                            .Where(c => c.BoardId == task.BoardId && c.Position > 0 && !c.IsDeleted)
                            .OrderBy(c => c.Position)
                            .FirstOrDefaultAsync();

                        if (firstColumn != null)
                        {
                            var lastTask = await _context.ProjectTasks
                                .Where(t => t.ColumnId == firstColumn.Id)
                                .OrderBy(t => t.Position)
                                .LastOrDefaultAsync();

                            task.ColumnId = firstColumn.Id;
                            task.Position = _lexorankService.GetInitialPosition(lastTask?.Position);

                            _context.TaskStatusHistories.Add(new TaskStatusHistory
                            {
                                Id = Guid.NewGuid(),
                                TaskId = task.Id,
                                ColumnId = firstColumn.Id,
                                CreatedAt = DateTime.UtcNow
                            });
                        }
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                foreach (var task in sprintTasks)
                {
                    if (task.BoardId.HasValue)
                    {
                        try
                        {
                            await _hubContext.Clients
                                .Group($"project-{projectId}")
                                .SendAsync("TaskMoved", new
                                {
                                    taskId = task.Id,
                                    boardId = task.BoardId,
                                    columnId = task.ColumnId,
                                    sprintId = task.SprintId,
                                    position = task.Position,
                                    completedAt = task.CompletedAt,
                                    rowVersion = task.xmin
                                });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                                "TaskMoved", projectId);
                        }
                    }
                }
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }

            try
            {
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("SprintUpdated", new
                    {
                        sprintId = sprint.Id,
                        state = sprint.State,
                        rowVersion = sprint.xmin
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                    "SprintUpdated", projectId);
            }

            try
            {
                var activity = await _activityService.LogActivityAsync(
                    projectId,
                    "Sprint",
                    sprint.Id,
                    "Activated",
                    $"{_currentUserService.DisplayName} aktiválta a {sprint.Name} sprintet"
                );
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("ActivityCreated", activity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: ActivityCreated | ProjectId: {ProjectId}", projectId);
            }

            return MapToDto(sprint);
        }

        public async Task<SprintResponseDto> CompleteSprintAsync(Guid projectId, Guid sprintId, Guid? targetSprintId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található");

            var sprint = await _context.Sprints.FirstOrDefaultAsync(s => s.Id == sprintId);
            if (sprint == null)
                throw new Exception("Sprint nem található");

            if (targetSprintId.HasValue)
            {
                var targetSprint = await _context.Sprints
                    .FirstOrDefaultAsync(s => s.Id == targetSprintId && s.ProjectId == projectId);
                if (targetSprint == null)
                    throw new Exception("A cél sprint nem található");
                if (targetSprint.State == SprintStates.Completed)
                    throw new Exception("A cél sprint már le van zárva!");
            }

            List<ProjectTask> unfinishedTasks = new();
            List<ProjectTask> completedTasks = new();

            using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                // Befejezetlen taskok ellenőrzése (CompletedAt alapján)
                unfinishedTasks = await _context.ProjectTasks
                    .Where(t => t.SprintId == sprintId && t.CompletedAt == null)
                    .ToListAsync();

                // Befejezetlen taskok kezelése
                if (unfinishedTasks.Count > 0)
                {
                    if (targetSprintId == null)
                    {
                        // Backlogba
                        foreach (var task in unfinishedTasks)
                        {
                            task.BoardId = null;
                            task.ColumnId = null;
                            task.SprintId = null;

                            _context.TaskStatusHistories.Add(new TaskStatusHistory
                            {
                                Id = Guid.NewGuid(),
                                TaskId = task.Id,
                                ColumnId = null,
                                CreatedAt = DateTime.UtcNow
                            });
                        }
                    }
                    else
                    {
                        string? lastPosition = null;

                        // Következő sprintbe
                        foreach (var task in unfinishedTasks)
                        {
                            task.SprintId = targetSprintId;
                            task.CompletedAt = null;

                            // Első oszlopba rakás ha van board
                            if (task.BoardId.HasValue)
                            {
                                var firstColumn = await _context.ColumnDefinitions
                                    .Where(c => c.BoardId == task.BoardId && c.Position > 0 && !c.IsDeleted)
                                    .OrderBy(c => c.Position)
                                    .FirstOrDefaultAsync();

                                if (firstColumn != null)
                                {
                                    if (lastPosition == null)
                                    {
                                        //Csak az első körnél nézzük meg a DB-t, utána lastPosition követés.
                                        var lastTask = await _context.ProjectTasks
                                            .Where(t => t.ColumnId == firstColumn.Id)
                                            .OrderBy(t => t.Position)
                                            .LastOrDefaultAsync();
                                        lastPosition = lastTask?.Position;
                                    }

                                    task.Position = _lexorankService.GetInitialPosition(lastPosition);
                                    lastPosition = task.Position;
                                    task.ColumnId = firstColumn.Id;

                                    _context.TaskStatusHistories.Add(new TaskStatusHistory
                                    {
                                        Id = Guid.NewGuid(),
                                        TaskId = task.Id,
                                        ColumnId = firstColumn.Id,
                                        CreatedAt = DateTime.UtcNow
                                    });
                                }
                            }
                        }
                    }
                }

                // ClosedAt beállítása CSAK a befejezett taskokra
                completedTasks = await _context.ProjectTasks
                    .Where(t => t.SprintId == sprintId && t.CompletedAt != null)
                    .ToListAsync();

                foreach (var task in completedTasks)
                {
                    task.ClosedAt = DateTime.UtcNow;
                    //Kész task már historyzálva van, itt csak a lezárás van kezelve.
                }

                sprint.State = SprintStates.Completed;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }

            foreach (var task in unfinishedTasks)
            {
                try
                {
                    await _hubContext.Clients
                        .Group($"project-{projectId}")
                        .SendAsync("TaskMoved", new
                        {
                            taskId = task.Id,
                            boardId = task.BoardId,
                            columnId = task.ColumnId,
                            sprintId = task.SprintId,
                            position = task.Position,
                            completedAt = task.CompletedAt,
                            rowVersion = task.xmin
                        });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                        "TaskMoved", projectId);
                }
            }

            foreach (var task in completedTasks)
            {
                try
                {
                    await _hubContext.Clients
                        .Group($"project-{projectId}")
                        .SendAsync("TaskMoved", new
                        {
                            taskId = task.Id,
                            boardId = task.BoardId,
                            columnId = task.ColumnId,
                            sprintId = task.SprintId,
                            position = task.Position,
                            completedAt = task.CompletedAt,
                            rowVersion = task.xmin
                        });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                        "TaskMoved", projectId);
                }
            }

            try
            {
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("SprintUpdated", new
                    {
                        sprintId = sprint.Id,
                        state = sprint.State,
                        rowVersion = sprint.xmin
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                    "SprintUpdated", projectId);
            }

            try
            {
                var activity = await _activityService.LogActivityAsync(
                    projectId,
                    "Sprint",
                    sprint.Id,
                    SprintStates.Completed,
                    $"{_currentUserService.DisplayName} lezárta a {sprint.Name} sprintet"
                );
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("ActivityCreated", activity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: ActivityCreated | ProjectId: {ProjectId}", projectId);
            }

            return MapToDto(sprint);
        }

        public async Task<SprintResponseDto> CreateSprintAsync(Guid projectId, CreateSprintDto dto)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található");

            var sprint = new Sprint
            {
                ProjectId = dto.ProjectId,
                Name = dto.Name,
                Goal = dto.Goal,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                State = dto.State,
            };
            _context.Sprints.Add(sprint);
            await _context.SaveChangesAsync();

            try
            {
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("SprintCreated", new
                    {
                        id = sprint.Id,
                        name = sprint.Name,
                        goal = sprint.Goal,
                        state = sprint.State,
                        startDate = sprint.StartDate,
                        endDate = sprint.EndDate,
                        createdAt = sprint.CreatedAt,
                        rowVersion = sprint.xmin
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                    "SprintCreated", projectId);
            }

            try
            {
                var activity = await _activityService.LogActivityAsync(
                    projectId,
                    "Sprint",
                    sprint.Id,
                    "Created",
                    $"{_currentUserService.DisplayName} létrehozta a {sprint.Name} sprintet"
                );
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("ActivityCreated", activity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: ActivityCreated | ProjectId: {ProjectId}", projectId);
            }

            return MapToDto(sprint);
        }

        public async Task DeleteSprintAsync(Guid projectId, Guid sprintId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található");

            var sprint = await _context.Sprints.FirstOrDefaultAsync(s => s.Id == sprintId);
            if (sprint == null)
                throw new Exception("Sprint nem található");

            var tasks = await _context.ProjectTasks.Where(t => t.SprintId == sprintId).ToListAsync();
            foreach ( var task in tasks )
            {
                task.SprintId = null;
            }
            _context.Sprints.Remove(sprint);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new Exception("A sprint időközben módosult, kérjük próbáld újra!");
            }

            try
            {
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("SprintDeleted", new { sprintId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                    "SprintDeleted", projectId);
            }

            try
            {
                var activity = await _activityService.LogActivityAsync(
                    projectId,
                    "Sprint",
                    sprint.Id,
                    "Deleted",
                    $"{_currentUserService.DisplayName} törölte a {sprint.Name} sprintet"
                );
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("ActivityCreated", activity);
            }
            catch { }
        }

        public async Task<List<SprintResponseDto>> GetSprintsAsync(
            Guid projectId, 
            string? scope = null)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található");

            var query = _context.Sprints
                .Where(s => s.ProjectId == projectId)
                .AsQueryable();

            if (scope == "initial")
            {
                // Aktív + Planning sprintek (kezdő betöltéshez)
                query = query.Where(s => s.State == SprintStates.Active || s.State == SprintStates.Planning);
            }
            else if (scope == "completed")
            {
                // Csak lezárt sprintek (SprintsView lazy load)
                query = query.Where(s => s.State == SprintStates.Completed);
            }
            // scope == null: összes sprint (backward compatibility)

            var sprints = await query.ToListAsync();

            return sprints.Select(MapToDto).ToList();
        }

        public async Task<List<TaskResponseDto>> GetUnfinishedTasksAsync(Guid projectId, Guid sprintId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található");

            var sprint = await _context.Sprints.FirstOrDefaultAsync(s => s.Id == sprintId);
            if (sprint == null)
                throw new Exception("Sprint nem található");

            var tasks = await _context.ProjectTasks
                .Where(t => t.SprintId == sprintId && t.CompletedAt == null)
                .Include(t => t.CreatedByUser)
                .Include(t => t.ColumnDefinition)
                .ToListAsync();
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

        public async Task<SprintResponseDto> PlanSprintAsync(Guid projectId, Guid sprintId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található");

            var sprint = await _context.Sprints.FirstOrDefaultAsync(s => s.Id == sprintId);
            if (sprint == null)
                throw new Exception("Sprint nem található");

            using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                sprint.State = SprintStates.Planning;

                var sprintTasks = await _context.ProjectTasks
                    .Where(t => t.SprintId == sprintId)
                    .ToListAsync();

                foreach (var task in sprintTasks)
                {
                    if (task.BoardId.HasValue)
                    {
                        // Backlog oszlop (Position=0) keresése
                        var backlogColumn = await _context.ColumnDefinitions
                            .FirstOrDefaultAsync(c => c.BoardId == task.BoardId && c.Position == 0);

                        if (backlogColumn != null)
                        {
                            var lastTask = await _context.ProjectTasks
                                .Where(t => t.ColumnId == backlogColumn.Id)
                                .OrderBy(t => t.Position)
                                .LastOrDefaultAsync();

                            task.ColumnId = backlogColumn.Id;
                            task.Position = _lexorankService.GetInitialPosition(lastTask?.Position);
                            task.CompletedAt = null;

                            _context.TaskStatusHistories.Add(new TaskStatusHistory
                            {
                                Id = Guid.NewGuid(),
                                TaskId = task.Id,
                                ColumnId = backlogColumn.Id,
                                CreatedAt = DateTime.UtcNow
                            });
                        }
                    }
                    // Ha nincs BoardId, már Projekt Backlogban van, nem kell mozgatni
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                foreach (var task in sprintTasks)
                {
                    if (task.BoardId.HasValue)
                    {
                        try
                        {
                            await _hubContext.Clients
                                .Group($"project-{projectId}")
                                .SendAsync("TaskMoved", new
                                {
                                    taskId = task.Id,
                                    boardId = task.BoardId,
                                    columnId = task.ColumnId,
                                    sprintId = task.SprintId,
                                    position = task.Position,
                                    completedAt = task.CompletedAt,
                                    rowVersion = task.xmin
                                });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                                "TaskMoved", projectId);
                        }
                    }
                }
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }

            try
            {
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("SprintUpdated", new
                    {
                        sprintId = sprint.Id,
                        state = sprint.State,
                        rowVersion = sprint.xmin
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                    "SprintUpdated", projectId);
            }

            try
            {
                var activity = await _activityService.LogActivityAsync(
                    projectId,
                    "Sprint",
                    sprint.Id,
                    "Replanned",
                    $"{_currentUserService.DisplayName} visszatervezte a {sprint.Name} sprintet"
                );
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("ActivityCreated", activity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: ActivityCreated | ProjectId: {ProjectId}", projectId);
            }

            return MapToDto(sprint);
        }

        public async Task<SprintResponseDto> UpdateSprintAsync(Guid projectId, Guid sprintId, UpdateSprintDto dto)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található");

            var sprint = await _context.Sprints.FirstOrDefaultAsync(s => s.Id == sprintId);
            if (sprint == null)
                throw new Exception("Sprint nem található");

            _context.Entry(sprint).OriginalValues["RowVersion"] = dto.RowVersion;

            if (dto.Name != null) sprint.Name = dto.Name;
            if (dto.Goal != null) sprint.Goal = dto.Goal;
            if (dto.StartDate != null) sprint.StartDate = dto.StartDate;
            if (dto.EndDate != null) sprint.EndDate = dto.EndDate;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new Exception("A sprint időközben módosult, kérjük próbáld újra!");
            }

            try
            {
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("SprintUpdated", new
                    {
                        sprintId = sprint.Id,
                        name = sprint.Name,
                        goal = sprint.Goal,
                        startDate = sprint.StartDate,
                        endDate = sprint.EndDate,
                        state = sprint.State,
                        rowVersion = sprint.xmin
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                    "SprintUpdated", projectId);
            }

            try
            {
                var activity = await _activityService.LogActivityAsync(
                    projectId,
                    "Sprint",
                    sprint.Id,
                    "Updated",
                    $"{_currentUserService.DisplayName} módosította a {sprint.Name} sprintet"
                );
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("ActivityCreated", activity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: ActivityCreated | ProjectId: {ProjectId}", projectId);
            }

            return MapToDto(sprint);
        }

        public async Task<TaskResponseDto> AssignTaskToSprintAsync(Guid projectId, Guid taskId, Guid sprintId, AssignTaskToSprintDto dto)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található");

            var task = await _context.ProjectTasks.FirstOrDefaultAsync(t => t.Id == taskId);
            if (task == null)
                throw new Exception("Task nem található");

            _context.Entry(task).OriginalValues["xmin"] = dto.RowVersion;

            var sprint = await _context.Sprints.FirstOrDefaultAsync(s => s.Id == sprintId);
            if (sprint == null)
                throw new Exception("Sprint nem található");

            if (task.BoardId.HasValue && sprint.State == SprintStates.Active)
            {
                var firstColumn = await _context.ColumnDefinitions
                    .Where(c => c.BoardId == task.BoardId && c.Position > 0 && !c.IsDeleted)
                    .OrderBy(c => c.Position)
                    .FirstOrDefaultAsync();

                if (firstColumn != null)
                {
                    var lastTask = await _context.ProjectTasks
                        .Where(t => t.ColumnId == firstColumn.Id)
                        .OrderBy(t => t.Position)
                        .LastOrDefaultAsync();

                    task.ColumnId = firstColumn.Id;
                    task.Position = _lexorankService.GetInitialPosition(lastTask?.Position);

                    _context.TaskStatusHistories.Add(new TaskStatusHistory
                    {
                        Id = Guid.NewGuid(),
                        TaskId = task.Id,
                        ColumnId = firstColumn.Id,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            task.SprintId = sprintId;

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
                        sprintId = task.SprintId,
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
                    "SprintAssigned",
                    $"{_currentUserService.DisplayName} sprinthez rendelte a {task.TaskKey} taskot"
                );
                await _hubContext.Clients
                    .Group($"project-{task.ProjectId}")
                    .SendAsync("ActivityCreated", activity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: ActivityCreated | ProjectId: {ProjectId}", projectId);
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

        public async Task RemoveTaskFromSprintAsync(Guid projectId, Guid taskId, AssignTaskToSprintDto dto)
        {
            var task = await _context.ProjectTasks.FirstOrDefaultAsync(t => t.Id == taskId);
            if (task == null)
                throw new Exception("Task nem található");

            _context.Entry(task).OriginalValues["xmin"] = dto.RowVersion;

            if (task.BoardId.HasValue)
            {
                var backlogColumn = await _context.ColumnDefinitions
                    .FirstOrDefaultAsync(c => c.BoardId == task.BoardId && c.Position == 0);

                if (backlogColumn != null)
                {
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

            task.CompletedAt = null;
            task.SprintId = null;

            if (!task.BoardId.HasValue)
            {
                _context.TaskStatusHistories.Add(new TaskStatusHistory
                {
                    Id = Guid.NewGuid(),
                    TaskId = task.Id,
                    ColumnId = null,
                    CreatedAt = DateTime.UtcNow
                });
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
                    .SendAsync("TaskUpdated", new
                    {
                        taskId = task.Id,
                        sprintId = (Guid?)null,
                        columnId = task.ColumnId,
                        position = task.Position,
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
                    "SprintAssigned",
                    $"{_currentUserService.DisplayName} visszatette a {task.TaskKey} taskot a backlogba"
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

        private SprintResponseDto MapToDto(Sprint sprint)
        {
            return new SprintResponseDto
            {
                Id = sprint.Id,
                ProjectId = sprint.ProjectId,
                Name = sprint.Name,
                Goal = sprint.Goal,
                StartDate = sprint.StartDate,
                EndDate = sprint.EndDate,
                State = sprint.State,
                CreatedAt = sprint.CreatedAt,
                UpdatedAt = sprint.UpdatedAt,
                RowVersion = sprint.xmin
            };
        }
    }
}
