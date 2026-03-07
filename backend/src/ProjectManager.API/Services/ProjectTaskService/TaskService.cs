using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Data;
using ProjectManager.API.DTOs.ProjectTask;
using ProjectManager.API.DTOs.Shared;
using ProjectManager.API.Model;

namespace ProjectManager.API.Services.ProjectTaskService
{
    public class TaskService : ITaskService
    {
        private readonly AppDbContext _context;

        public TaskService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<TaskResponseDto> CreateTaskAsync(Guid createdById, Guid projectId, CreateTaskDto dto)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == createdById);
            if (user == null)
                throw new Exception("Felhasználó nem található!");

            var column = await _context.ColumnDefinitions.FirstOrDefaultAsync(cd => cd.Id == dto.ColumnId);
            if (column == null)
                throw new Exception("Oszlop nem található!");

            var counter = await _context.ProjectCounters.FirstOrDefaultAsync(pc => pc.ProjectId == projectId);
            if (counter == null)
                throw new Exception("Számláló nem található");

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
                Status = column.MapsToStatus,
                Priority = dto.Priority,
                Position = 0f,
                EstimateInMinutes = dto.EstimateInMinutes ?? 0,
                DueDate = dto.DueDate,
            };
            await _context.ProjectTasks.AddAsync(task);
            await _context.SaveChangesAsync();
            
            var response = new TaskResponseDto
            {
                Id = task.Id,
                ProjectId = task.ProjectId,
                BoardId= task.BoardId,
                ColumnId = task.ColumnId,
                SprintId = task.SprintId,
                AssigneeNames = new List<string>(),
                LabelNames = new List<string>(),
                CommitLinks = new List<string>(),
                PrLinks = new List<string>(),
                CreatedByName = user.DisplayName,
                TaskKey = task.TaskKey,
                Title = task.Title,
                Description = task.Description,
                Status = task.Status,
                Priority = task.Priority,
                Position = task.Position,
                EstimateInMinutes = task.EstimateInMinutes,
                DueDate = task.DueDate,
                ClosedAt = task.ClosedAt,
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
                .ToListAsync();

            //Magára a tasks listára query - Id- kinyerése
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
                Status = t.Status,
                Priority = t.Priority,
                Position = t.Position,
                EstimateInMinutes = t.EstimateInMinutes,
                DueDate = t.DueDate,
                ClosedAt = t.ClosedAt,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            }).ToList();
        }

        public async Task<TaskResponseDto> GetTaskByIdAsync(Guid taskId, Guid projectId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található");

            var task = await _context.ProjectTasks.FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId);
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

            var labelNames = await _context.LabelTasks
                .Where(lt => lt.TaskId == task.Id)
                .Include(lt => lt.Label)
                .Select(lt => lt.Label.Name)
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
                LabelNames = labelNames,
                CommitLinks = commitLinks,
                PrLinks = prLinks,
                Attachments = attachments,
                CreatedByName = user.DisplayName,
                TaskKey = task.TaskKey,
                Title = task.Title,
                Description = task.Description,
                Status = task.Status,
                Priority = task.Priority,
                Position = task.Position,
                EstimateInMinutes = task.EstimateInMinutes,
                DueDate = task.DueDate,
                ClosedAt = task.ClosedAt,
                CreatedAt = task.CreatedAt,
                UpdatedAt = task.UpdatedAt
            };
            return response;
        }

        public async Task<TaskResponseDto> MoveTaskAsync(Guid taskId, MoveTaskDto dto)
        {
            var task = await _context.ProjectTasks.FirstOrDefaultAsync(t => t.Id == taskId);
            if (task == null)
                throw new Exception("Feladat nem található");
            

            var column = await _context.ColumnDefinitions.FirstOrDefaultAsync(cd => cd.Id == dto.ColumnId);
            if (column == null)
                throw new Exception("Oszlop nem található");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == task.CreatedById);
            if (user == null)
                throw new Exception("Felhasználó nem található!");

            task.ColumnId = dto.ColumnId;
            task.Position = dto.Position;
            task.Status = column.MapsToStatus;
            await _context.SaveChangesAsync();

            var assigneeNames = await _context.TaskAssignments
                .Where(ta => ta.TaskId == task.Id)
                .Include(ta => ta.User)
                .Select(ta => ta.User.DisplayName)
                .ToListAsync();

            var labelNames = await _context.LabelTasks
                .Where(lt => lt.TaskId == task.Id)
                .Include(lt => lt.Label)
                .Select(lt => lt.Label.Name)
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
                LabelNames = labelNames,
                CommitLinks = commitLinks,
                PrLinks = prLinks,
                CreatedByName = user.DisplayName,
                TaskKey = task.TaskKey,
                Title = task.Title,
                Description = task.Description,
                Status = task.Status,
                Priority = task.Priority,
                Position = task.Position,
                EstimateInMinutes = task.EstimateInMinutes,
                DueDate = task.DueDate,
                ClosedAt = task.ClosedAt,
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

            var assigneeNames = await _context.TaskAssignments
                .Where(ta => ta.TaskId == task.Id)
                .Include(ta => ta.User)
                .Select(ta => ta.User.DisplayName)
                .ToListAsync();

            var labelNames = await _context.LabelTasks
                .Where(lt => lt.TaskId == task.Id)
                .Include(lt => lt.Label)
                .Select(lt => lt.Label.Name)
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
                LabelNames = labelNames,
                CommitLinks = commitLinks,
                PrLinks = prLinks,
                CreatedByName = user.DisplayName,
                TaskKey = task.TaskKey,
                Title = task.Title,
                Description = task.Description,
                Status = task.Status,
                Priority = task.Priority,
                Position = task.Position,
                EstimateInMinutes = task.EstimateInMinutes,
                DueDate = task.DueDate,
                ClosedAt = task.ClosedAt,
                CreatedAt = task.CreatedAt,
                UpdatedAt = task.UpdatedAt
            };
            return response;
        }
    }
}
