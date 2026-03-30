using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Data;
using ProjectManager.API.DTOs.ProjectTask;
using ProjectManager.API.DTOs.Shared;
using ProjectManager.API.DTOs.Sprints;
using ProjectManager.API.Model;

namespace ProjectManager.API.Services.SprintService
{
    public class SprintService : ISprintService
    {
        private readonly AppDbContext _context;
        //Status:
        //"Planning"
        //"Active"
        //"Completed"

        public SprintService(AppDbContext context)
        {
            _context = context;
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
            await _context.SaveChangesAsync();

            var response = new SprintResponseDto
            {
                Id = sprint.Id,
                ProjectId = sprint.ProjectId,
                BoardId = sprint.BoardId,
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

            //Minden Boardban kell hogy szerepeljen egy done oszlop! (Projekt Creation esetén létrehozva alap esetben)
            var tasks = await _context.ProjectTasks
                .Where(t => t.SprintId == sprintId &&
                           (t.ColumnDefinition == null || t.ColumnDefinition.MapsToStatus != "Done"))
                .ToListAsync();

            if (tasks.Count == 0)
            {
                sprint.State = "Completed";
            }
            else if (targetSprintId == null)
            {
                var board = await _context.Boards.FirstOrDefaultAsync(b => b.Id == tasks[0].BoardId);
                if (board == null)
                    throw new Exception("Board nem található");
                var backlogColId = await _context.ColumnDefinitions
                    .Where(c => c.BoardId == board.Id && c.MapsToStatus == "Backlog") 
                    //Minden Boardban kell hogy szerepeljen egy backlog oszlop!
                    .Select(c => c.Id)
                    .FirstOrDefaultAsync();
                foreach (var task in tasks)
                {
                    task.ColumnId = backlogColId;
                    task.SprintId = null;
                }
                sprint.State = "Completed";
            }
            else
            {
                foreach (var task in tasks)
                {
                    task.SprintId = targetSprintId;
                }
                sprint.State = "Completed";
            }
            
            await _context.SaveChangesAsync();

            var response = new SprintResponseDto
            {
                Id = sprint.Id,
                ProjectId = sprint.ProjectId,
                BoardId = sprint.BoardId,
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

        public async Task<SprintResponseDto> CreateSprintAsync(Guid projectId, CreateSprintDto dto)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található");

            var sprint = new Sprint
            {
                ProjectId = dto.ProjectId,
                BoardId = dto.BoardId,
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
                BoardId = sprint.BoardId,
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
                BoardId = s.BoardId,
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
                .Where(t => t.SprintId == sprintId &&
                           (t.ColumnDefinition == null || t.ColumnDefinition.MapsToStatus != "Done"))
                .Include(t => t.CreatedByUser)
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
                CreatedByName = t.CreatedByUser.DisplayName,
                TaskKey = t.TaskKey,
                Title = t.Title,
                Description = t.Description,
                Status = t.ColumnDefinition.MapsToStatus ?? "Backlog",
                Priority = t.Priority,
                Position = t.Position,
                EstimateInMinutes = t.EstimateInMinutes,
                DueDate = t.DueDate,
                ClosedAt = t.ClosedAt,
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
            await _context.SaveChangesAsync();

            var response = new SprintResponseDto
            {
                Id = sprint.Id,
                ProjectId = sprint.ProjectId,
                BoardId = sprint.BoardId,
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

            if(dto.BoardId != null) sprint.BoardId = dto.BoardId;
            if(dto.Name != null) sprint.Name = dto.Name;
            if(dto.Goal != null) sprint.Goal = dto.Goal;
            if(dto.StartDate != null) sprint.StartDate = dto.StartDate;
            if(dto.EndDate != null) sprint.EndDate = dto.EndDate;

            await _context.SaveChangesAsync();
            var response = new SprintResponseDto
            {
                Id = sprint.Id,
                ProjectId = sprint.ProjectId,
                BoardId = sprint.BoardId,
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
            }

            // null = vissza Backlogba
            task.SprintId = sprintId; 
            await _context.SaveChangesAsync();
        }
    }
}
