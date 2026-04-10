using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using ProjectManager.API.Data;
using ProjectManager.API.DTOs.ProjectTask;
using ProjectManager.API.DTOs.Shared;
using ProjectManager.API.Hubs;
using ProjectManager.API.Model;
using ProjectManager.API.Services.CurrentUserService;
using ProjectManager.API.Services.LexorankService;


namespace ProjectManager.API.Services.ProjectTaskService
{
    public class TaskService : ITaskService
    {
        private readonly AppDbContext _context;
        private readonly ILexorankService _lexorankService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IHubContext<ProjectHub> _hubContext;

        public TaskService(AppDbContext context, ILexorankService lexorankService, ICurrentUserService currentUserService, IHubContext<ProjectHub> hubContext)
        {
            _context = context;
            _lexorankService = lexorankService;
            _currentUserService = currentUserService;
            _hubContext = hubContext;
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
                    .FirstOrDefaultAsync(cd => cd.Id == dto.ColumnId);
                if (column == null)
                    throw new Exception("Oszlop nem található!");
            }

            var counter = await _context.ProjectCounters.FirstOrDefaultAsync(pc => pc.ProjectId == projectId);
            if (counter == null)
                throw new Exception("Számláló nem található");

            var lastTask = dto.ColumnId.HasValue
                ? await _context.ProjectTasks
                    .Where(t => t.ColumnId == dto.ColumnId)
                    .OrderBy(t => t.Position)
                    .LastOrDefaultAsync()
                : null;

            counter.LastNum += 1;
            var taskKey = $"{project.ProjKey}-{counter.LastNum}";

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
            };
            await _context.ProjectTasks.AddAsync(task);
            await _context.SaveChangesAsync();
            await _hubContext.Clients
                .Group($"project-{projectId}")
                .SendAsync("TaskCreated", new
                {
                    task.Id,
                    task.BoardId,
                    task.ColumnId,
                    task.SprintId,
                    task.Title,
                    task.TaskKey
                });

            var response = new TaskResponseDto
            {
                Id = task.Id,
                ProjectId = task.ProjectId,
                BoardId= task.BoardId,
                ColumnId = task.ColumnId,
                SprintId = task.SprintId,
                AssigneeNames = new List<string>(),
                LabelIds = new List<string>(),
                CommitLinks = new List<string>(),
                PrLinks = new List<string>(),
                CreatedByName = user.DisplayName,
                TaskKey = task.TaskKey,
                Title = task.Title,
                Description = task.Description,
                Status = task.ColumnDefinition?.MapsToStatus ?? "Backlog",
                Priority = task.Priority,
                Position = task.Position,
                EstimateInMinutes = task.EstimateInMinutes,
                DueDate = task.DueDate,
                ClosedAt = task.ClosedAt,
                CompletedAt = task.CompletedAt,
                CreatedAt = task.CreatedAt,
                UpdatedAt = task.UpdatedAt
            };
            return response;
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
            await _context.SaveChangesAsync();
            await _hubContext.Clients
                .Group($"project-{task.ProjectId}")
                .SendAsync("TaskDeleted", new
                {
                    taskId
                });
        }

        public async Task<List<TaskResponseDto>> GetTasksAsync(Guid projectId, Guid? boardId = null, Guid? sprintId = null)
        {
            //Jövőbeli fejlesztés: Lapozás

            //tasks Lista
            var tasks = await _context.ProjectTasks
                .Where(t => t.ProjectId == projectId)
                .Where(t => boardId == null || t.BoardId == boardId)
                .Where(t => sprintId == null || t.SprintId == sprintId)
                .Include(t => t.CreatedByUser)
                .Include(t => t.ColumnDefinition)
                .ToListAsync();

            //Magára a tasks listára query - Id- kinyerése
            var taskIds = tasks.Select(t => t.Id).ToList();

            //Id alapján az 5 listát feltöltjük
            var assignments = await _context.TaskAssignments
                .Where(ta => taskIds.Contains(ta.TaskId))
                .Include(ta => ta.User)
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
                .ToListAsync();


            return tasks.Select(t => new TaskResponseDto
            {
                Id = t.Id,
                ProjectId = t.ProjectId,
                BoardId = t.BoardId,
                ColumnId = t.ColumnId,
                SprintId = t.SprintId,
                //Taskonként kinyerjük a listákból csak az adotthoz hozzátartozó lista bejegyzéseket
                AssigneeNames = assignments
                    .Where(ta => ta.TaskId == t.Id)
                    .Select(ta => ta.User.DisplayName)
                    .ToList(),
                LabelIds = labels
                    .Where(lt => lt.TaskId == t.Id)
                    .Select(lt => lt.LabelId.ToString())
                    .ToList(),
                CommitLinks = commitLinks
                    .Where(cl => cl.TaskId == t.Id)
                    .Select(cl => cl.CommitUrl ?? cl.CommitSha)
                    .ToList(),
                PrLinks = prLinks
                    .Where(pl => pl.TaskId == t.Id)
                    .Select(pl => pl.PrUrl ?? $"{pl.RepoFullName}#{pl.PrNumber}")
                    .ToList(),
                Attachments = attachments
                    .Where(a => a.TaskId == t.Id)
                    .Select(a => new AttachmentDto
                    {
                        Id = a.Id,
                        FileName = a.FileName,
                        FileSizeBytes = a.SizeBytes
                    })
                    .ToList(),
                CreatedByName = t.CreatedByUser.DisplayName,
                TaskKey = t.TaskKey,
                Title = t.Title,
                Description = t.Description,
                Status = t.ColumnDefinition?.MapsToStatus ?? "Backlog",
                Priority = t.Priority,
                Position = t.Position,
                EstimateInMinutes = t.EstimateInMinutes,
                DueDate = t.DueDate,
                ClosedAt = t.ClosedAt,
                CompletedAt = t.CompletedAt,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            }).ToList();
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

            var assigneeNames = await _context.TaskAssignments
                .Where(ta => ta.TaskId == task.Id)
                .Include(ta => ta.User)
                .Select(ta => ta.User.DisplayName)
                .ToListAsync();

            var labels = await _context.LabelTasks
                .Where(lt => lt.TaskId == task.Id)
                .Select(lt => lt.LabelId.ToString())
                .ToListAsync();

            var commitLinks = await _context.CommitLinks
                .Where(cl => cl.TaskId == task.Id)
                .Select(cl => cl.CommitUrl ?? cl.CommitSha)
                .ToListAsync();

            var prLinks = await _context.PrLinks
                .Where(pl => pl.TaskId == task.Id)
                .Select(pl => pl.PrUrl ?? $"{pl.RepoFullName}#{pl.PrNumber}")
                .ToListAsync();

            var attachments = await _context.Attachments
                .Where(a => a.TaskId == task.Id)
                .Select(a => new AttachmentDto
                {
                    Id = a.Id,
                    FileName = a.FileName,
                    FileSizeBytes = a.SizeBytes
                })
                .ToListAsync();

            var response = new TaskResponseDto
            {
                Id = task.Id,
                ProjectId = task.ProjectId,
                BoardId = task.BoardId,
                ColumnId = task.ColumnId,
                SprintId = task.SprintId,
                AssigneeNames = assigneeNames,
                LabelIds = labels,
                CommitLinks = commitLinks,
                PrLinks = prLinks,
                Attachments = attachments,
                CreatedByName = task.CreatedByUser?.DisplayName ?? "Ismeretlen",
                TaskKey = task.TaskKey,
                Title = task.Title,
                Description = task.Description,
                Status = task.ColumnDefinition?.MapsToStatus ?? "Backlog",
                Priority = task.Priority,
                Position = task.Position,
                EstimateInMinutes = task.EstimateInMinutes,
                DueDate = task.DueDate,
                ClosedAt = task.ClosedAt,
                CompletedAt = task.CompletedAt,
                CreatedAt = task.CreatedAt,
                UpdatedAt = task.UpdatedAt
            };
            return response;
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
            
            ColumnDefinition? column = null;
            if (dto.ColumnId.HasValue)
            {
                column = await _context.ColumnDefinitions
                    .FirstOrDefaultAsync(cd => cd.Id == dto.ColumnId);
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
                // Első helyre kerül, legkisebb pozíciójú task
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

            if (dto.ColumnId.HasValue &&
                prevTask != null && 
                nextTask != null &&
                _lexorankService.HasCollision(prevTask.Position, nextTask.Position) )
            {
                await RebalanceColumnAsync(dto.ColumnId.Value, prevTask.Position);       

                // Újra lekérés rebalancing után
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

            // Backend számítja a pozíciót
            var newPosition = _lexorankService.GetMiddle(
                prevTask?.Position,
                nextTask?.Position
            );
            
            task.Position = newPosition;
            task.ColumnId = dto.ColumnId;
            task.BoardId = column?.BoardId;
            // Ellenőrzés: utolsó oszlop-e? (CompletedAt beállítása)
            if (task.BoardId.HasValue)
            {
                var lastColumn = await _context.ColumnDefinitions
                    .Where(c => c.BoardId == task.BoardId)
                    .OrderByDescending(c => c.Position)
                    .FirstOrDefaultAsync();

                if (lastColumn != null && task.ColumnId == lastColumn.Id)
                    task.CompletedAt = DateTime.UtcNow;
                else
                    task.CompletedAt = null; // ha visszamozgatják akkor törlődik az időpont
            }

            await _context.SaveChangesAsync();
            await _hubContext.Clients
                .Group($"board-{task.BoardId}")
                .SendAsync("TaskMoved", new
                {
                    taskId = task.Id,
                    columnId = task.ColumnId,
                    position = task.Position,
                    triggeredBy = task.CreatedById
                });

            if (dto.ColumnId.HasValue && _lexorankService.NeedsRebalancing(newPosition))
            {
                await RebalanceColumnAsync(dto.ColumnId.Value, newPosition);
            }

            var assigneeNames = await _context.TaskAssignments
                .Where(ta => ta.TaskId == task.Id)
                .Include(ta => ta.User)
                .Select(ta => ta.User.DisplayName)
                .ToListAsync();

            var labels = await _context.LabelTasks
                 .Where(lt => lt.TaskId == task.Id)
                 .Select(lt => lt.LabelId.ToString())
                 .ToListAsync();

            var commitLinks = await _context.CommitLinks
                .Where(cl => cl.TaskId == task.Id)
                .Select(cl => cl.CommitUrl ?? cl.CommitSha)
                .ToListAsync();

            var prLinks = await _context.PrLinks
                .Where(pl => pl.TaskId == task.Id)
                .Select(pl => pl.PrUrl ?? $"{pl.RepoFullName}#{pl.PrNumber}")
                .ToListAsync();

            var response = new TaskResponseDto
            {
                Id = task.Id,
                ProjectId = task.ProjectId,
                BoardId = task.BoardId,
                ColumnId = task.ColumnId,
                SprintId = task.SprintId,
                AssigneeNames = assigneeNames,
                LabelIds = labels,
                CommitLinks = commitLinks,
                PrLinks = prLinks,
                CreatedByName = task.CreatedByUser.DisplayName,
                TaskKey = task.TaskKey,
                Title = task.Title,
                Description = task.Description,
                Status = task.ColumnDefinition?.MapsToStatus ?? "Backlog",
                Priority = task.Priority,
                Position = task.Position,
                EstimateInMinutes = task.EstimateInMinutes,
                DueDate = task.DueDate,
                ClosedAt = task.ClosedAt,
                CompletedAt = task.CompletedAt,
                CreatedAt = task.CreatedAt,
                UpdatedAt = task.UpdatedAt
            };
            return response;
        }

        public async Task<TaskResponseDto> UpdateTaskAsync(Guid taskId, UpdateTaskDto dto)
        {
            var task = await _context.ProjectTasks.FirstOrDefaultAsync(t => t.Id == taskId);
            if (task == null)
                throw new Exception("Feladat nem található");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == task.CreatedById);
            if (user == null)
                throw new Exception("Felhasználó nem található!");

            if (dto.Title != null) task.Title = dto.Title;
            if(dto.Description != null) task.Description = dto.Description;
            if(dto.BoardId != null) task.BoardId = dto.BoardId.Value;
            if(dto.SprintId != null) task.SprintId = dto.SprintId;
            if(dto.Priority != null) task.Priority = dto.Priority;
            if (dto.EstimateInMinutes.HasValue) task.EstimateInMinutes = dto.EstimateInMinutes.Value;
            if (dto.DueDate != null) task.DueDate = dto.DueDate;

            await _context.SaveChangesAsync();
            await _hubContext.Clients
                .Group($"project-{task.ProjectId}")
                .SendAsync("TaskUpdated", new
                {
                    taskId = task.Id,
                    title = task.Title,
                    priority = task.Priority,
                    dueDate = task.DueDate
                });

            var assigneeNames = await _context.TaskAssignments
                .Where(ta => ta.TaskId == task.Id)
                .Include(ta => ta.User)
                .Select(ta => ta.User.DisplayName)
                .ToListAsync();

            var labels = await _context.LabelTasks
                .Where(lt => lt.TaskId == task.Id)
                .Select(lt => lt.LabelId.ToString())
                .ToListAsync();

            var commitLinks = await _context.CommitLinks
                .Where(cl => cl.TaskId == task.Id)
                .Select(cl => cl.CommitUrl ?? cl.CommitSha)
                .ToListAsync();

            var prLinks = await _context.PrLinks
                .Where(pl => pl.TaskId == task.Id)
                .Select(pl => pl.PrUrl ?? $"{pl.RepoFullName}#{pl.PrNumber}")
                .ToListAsync();

            var response = new TaskResponseDto
            {
                Id = task.Id,
                ProjectId = task.ProjectId,
                BoardId = task.BoardId,
                ColumnId = task.ColumnId,
                SprintId = task.SprintId,
                AssigneeNames = assigneeNames,
                LabelIds = labels,
                CommitLinks = commitLinks,
                PrLinks = prLinks,
                CreatedByName = user.DisplayName,
                TaskKey = task.TaskKey,
                Title = task.Title,
                Description = task.Description,
                Status = task.ColumnDefinition?.MapsToStatus ?? "Backlog",
                Priority = task.Priority,
                Position = task.Position,
                EstimateInMinutes = task.EstimateInMinutes,
                DueDate = task.DueDate,
                ClosedAt = task.ClosedAt,
                CompletedAt = task.CompletedAt,
                CreatedAt = task.CreatedAt,
                UpdatedAt = task.UpdatedAt
            };
            return response;
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

            if (!dto.BoardId.HasValue)
            {
                task.BoardId = null;
                task.ColumnId = null;
                task.Position = string.Empty;
                task.CompletedAt = null;
                await _context.SaveChangesAsync();
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
                            .Where(c => c.BoardId == dto.BoardId && c.Position > 0)
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
                await _context.SaveChangesAsync();
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("TaskUpdated", new
                    {
                        taskId = task.Id,
                        boardId = task.BoardId,
                        columnId = task.ColumnId,
                        position = task.Position
                    });
            }

            // Response összerakása
            var assigneeNames = await _context.TaskAssignments
                .Where(ta => ta.TaskId == task.Id)
                .Include(ta => ta.User)
                .Select(ta => ta.User.DisplayName)
                .ToListAsync();

            var labels = await _context.LabelTasks
                .Where(lt => lt.TaskId == task.Id)
                .Select(lt => lt.LabelId.ToString())
                .ToListAsync();

            return new TaskResponseDto
            {
                Id = task.Id,
                ProjectId = task.ProjectId,
                BoardId = task.BoardId,
                ColumnId = task.ColumnId,
                SprintId = task.SprintId,
                AssigneeNames = assigneeNames,
                LabelIds = labels,
                CommitLinks = new List<string>(),
                PrLinks = new List<string>(),
                CreatedByName = task.CreatedByUser.DisplayName,
                TaskKey = task.TaskKey,
                Title = task.Title,
                Description = task.Description,
                Status = task.ColumnDefinition?.MapsToStatus ?? "Backlog",
                Priority = task.Priority,
                Position = task.Position,
                EstimateInMinutes = task.EstimateInMinutes,
                DueDate = task.DueDate,
                ClosedAt = task.ClosedAt,
                CompletedAt = task.CompletedAt,
                CreatedAt = task.CreatedAt,
                UpdatedAt = task.UpdatedAt
            };
        }

        private async Task RebalanceColumnAsync(Guid columnId, string position)
        {
            var column = await _context.ColumnDefinitions
                .FirstOrDefaultAsync(c => c.Id == columnId);
            
            var bucket = _lexorankService.GetBucket(position);
            var nextBucket = _lexorankService.GetNextBucket(bucket);

            var allTasksInColumn = await _context.ProjectTasks
                .Where(t => t.ColumnId == columnId)
                .OrderBy(t => t.Position)
                .ToListAsync();

            var newPositions = _lexorankService.RebalancePositions(
                allTasksInColumn.Count,
                nextBucket
            );

            for (int i = 0; i < allTasksInColumn.Count; i++)
            {
                allTasksInColumn[i].Position = newPositions[i];
            }

            await _context.SaveChangesAsync();
            await _hubContext.Clients
                .Group($"board-{column!.BoardId}")
                .SendAsync("TasksRebalanced", new
                {
                    columnId,
                    tasks = allTasksInColumn.Select(t => new { t.Id, t.Position })
                });
        }
    }
}
