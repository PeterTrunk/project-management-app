using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Data;
using ProjectManager.API.DTOs.ProjectTask;
using ProjectManager.API.DTOs.Shared;
using ProjectManager.API.DTOs.Sprints;
using ProjectManager.API.Model;
using ProjectManager.API.Services.LexorankService;

namespace ProjectManager.API.Services.SprintService
{
    public class SprintService : ISprintService
    {
        private readonly AppDbContext _context;
        private readonly ILexorankService _lexorankService;
        //Status:
        //"Planning"
        //"Active"
        //"Completed"

        public SprintService(AppDbContext context, ILexorankService lexorankService)
        {
            _context = context;
            _lexorankService = lexorankService;
        }

        public async Task<SprintResponseDto> ActivateSprintAsync(Guid projectId, Guid sprintId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található");

            var sprint = await _context.Sprints.FirstOrDefaultAsync(s => s.Id == sprintId);
            if (sprint == null)
                throw new Exception("Sprint nem található");

            sprint.State = "Active";

            var sprintTasks = await _context.ProjectTasks
                .Where(t => t.SprintId == sprintId)
                .ToListAsync();

            foreach (var task in sprintTasks)
            {
                if (task.BoardId.HasValue)
                {
                    var firstColumn = await _context.ColumnDefinitions
                        .Where(c => c.BoardId == task.BoardId && c.Position > 0)
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
                    }
                }
            }

            await _context.SaveChangesAsync();

            var response = new SprintResponseDto
            {
                Id = sprint.Id,
                ProjectId = sprint.ProjectId,
                Name = sprint.Name,
                Goal = sprint.Goal,
                StartDate = sprint.StartDate,
                EndDate = sprint.EndDate,
                State = sprint.State,
                CreatedAt = sprint.CreatedAt,
                UpdatedAt = sprint.UpdatedAt
            };
            return response;
        }

        public async Task<SprintResponseDto> CompleteSprintAsync(Guid projectId, Guid sprintId, Guid? targetSprintId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található");

            var sprint = await _context.Sprints.FirstOrDefaultAsync(s => s.Id == sprintId);
            if (sprint == null)
                throw new Exception("Sprint nem található");

            // Befejezetlen taskok ellenőrzése (CompletedAt alapján)
            var unfinishedTasks = await _context.ProjectTasks
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
                    }
                }
                else
                {
                    // Következő sprintbe
                    foreach (var task in unfinishedTasks)
                    {
                        task.SprintId = targetSprintId;
                    }
                }
            }

            // ClosedAt beállítása CSAK a befejezett taskokra
            var completedTasks = await _context.ProjectTasks
                .Where(t => t.SprintId == sprintId && t.CompletedAt != null)
                .ToListAsync();

            foreach (var task in completedTasks)
            {
                task.ClosedAt = DateTime.UtcNow;
            }

            sprint.State = "Completed";
            await _context.SaveChangesAsync();

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
                UpdatedAt = sprint.UpdatedAt
            };
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

            var response = new SprintResponseDto
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
            };
            return response;
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
            await _context.SaveChangesAsync();
        }

        public async Task<List<SprintResponseDto>> GetSprintsAsync(Guid projectId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található");

            var sprints = await _context.Sprints.Where(s => s.ProjectId == projectId).ToListAsync();

            return sprints.Select(s => new SprintResponseDto
            {
                Id = s.Id,
                ProjectId = s.ProjectId,
                Name = s.Name,
                Goal = s.Goal,
                StartDate = s.StartDate,
                EndDate = s.EndDate,
                State = s.State,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt
            }).ToList();
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
                .Include(ta => ta.User)
                .ToListAsync();

            var labelTasks = await _context.LabelTasks
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
                LabelNames = labelTasks
                    .Where(lt => lt.TaskId == t.Id)
                    .Select(lt => lt.Label.Name)
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
                CreatedByName = t.CreatedByUser?.DisplayName ?? "Ismeretlen",
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

        public async Task<SprintResponseDto> PlanSprintAsync(Guid projectId, Guid sprintId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található");

            var sprint = await _context.Sprints.FirstOrDefaultAsync(s => s.Id == sprintId);
            if (sprint == null)
                throw new Exception("Sprint nem található");

            sprint.State = "Planning";

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
                    }
                }
                // Ha nincs BoardId → már Projekt Backlogban van → nem kell mozgatni
            }

            await _context.SaveChangesAsync();

            var response = new SprintResponseDto
            {
                Id = sprint.Id,
                ProjectId = sprint.ProjectId,
                Name = sprint.Name,
                Goal = sprint.Goal,
                StartDate = sprint.StartDate,
                EndDate = sprint.EndDate,
                State = sprint.State,
                CreatedAt = sprint.CreatedAt,
                UpdatedAt = sprint.UpdatedAt
            };
            return response;
        }

        public async Task<SprintResponseDto> UpdateSprintAsync(Guid projectId, Guid sprintId, UpdateSprintDto dto)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található");

            var sprint = await _context.Sprints.FirstOrDefaultAsync(s => s.Id == sprintId);
            if (sprint == null)
                throw new Exception("Sprint nem található");
            
            if(dto.Name != null) sprint.Name = dto.Name;
            if(dto.Goal != null) sprint.Goal = dto.Goal;
            if(dto.StartDate != null) sprint.StartDate = dto.StartDate;
            if(dto.EndDate != null) sprint.EndDate = dto.EndDate;

            await _context.SaveChangesAsync();
            var response = new SprintResponseDto
            {
                Id = sprint.Id,
                ProjectId = sprint.ProjectId,
                Name = sprint.Name,
                Goal = sprint.Goal,
                StartDate = sprint.StartDate,
                EndDate = sprint.EndDate,
                State = sprint.State,
                CreatedAt = sprint.CreatedAt,
                UpdatedAt = sprint.UpdatedAt
            };
            return response;
        }

        public async Task AssignTaskToSprintAsync(Guid projectId, Guid taskId, Guid? sprintId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található");

            var task = await _context.ProjectTasks.FirstOrDefaultAsync(t => t.Id == taskId);
            if (task == null)
                throw new Exception("Task nem található");
            
            if (sprintId.HasValue)
            {
                var sprint = await _context.Sprints.FirstOrDefaultAsync(s => s.Id == sprintId);
                if (sprint == null)
                    throw new Exception("Sprint nem található");

                if (task.BoardId.HasValue)
                {
                    if (sprint?.State == "Active")
                    {
                        var firstColumn = await _context.ColumnDefinitions
                            .Where(c => c.BoardId == task.BoardId && c.Position > 0)
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
                        }
                    }
                }
            }
            else
            {
                // Backlogba visszarakás
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
                    }
                }
                task.CompletedAt = null;
            }
            // null = vissza Backlogba
            task.SprintId = sprintId;
            await _context.SaveChangesAsync();
        }
    }
}
