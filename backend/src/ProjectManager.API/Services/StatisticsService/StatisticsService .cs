using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Data;
using ProjectManager.API.DTOs.Statistics;

namespace ProjectManager.API.Services.StatisticsService
{
    public class StatisticsService : IStatisticsService
    {
        private readonly AppDbContext _context;

        public StatisticsService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<BurndownDataPointDto>> GetBurndownAsync(Guid projectId, Guid sprintId)
        {
            var sprint = await _context.Sprints
                .FirstOrDefaultAsync(s => s.Id == sprintId && s.ProjectId == projectId);
            if (sprint == null)
                throw new Exception("Sprint nem található!");

            var startDate = sprint.StartDate ?? DateTime.UtcNow.AddDays(-14);
            var endDate = sprint.EndDate ?? DateTime.UtcNow;

            var tasks = await _context.ProjectTasks
                .Where(t => t.SprintId == sprintId)
                .ToListAsync();

            var totalTasks = tasks.Count;
            var result = new List<BurndownDataPointDto>();

            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                var completedByDate = tasks.Count(t =>
                    t.CompletedAt.HasValue && t.CompletedAt.Value.Date <= date);

                result.Add(new BurndownDataPointDto
                {
                    Date = date,
                    RemainingTasks = totalTasks - completedByDate,
                    TotalTasks = totalTasks,
                    CompletedTasks = completedByDate
                });
            }

            return result;
        }

        public async Task<List<CumulativeFlowDataPointDto>> GetCumulativeFlowAsync(Guid projectId, DateTime dateFrom, DateTime dateTo)
        {
            // Projekt összes oszlopának lekérése
            var columns = await _context.ColumnDefinitions
                .Where(c => c.Board.ProjectId == projectId && c.Position > 0)
                .Include(c => c.Board)
                .ToListAsync();

            var statuses = columns
                .Select(c => c.MapsToStatus)
                .Distinct()
                .ToList();
            statuses.Add("Backlog");

            // TaskStatusHistory lekérése a dátum intervallumra
            var histories = await _context.TaskStatusHistories
                .Where(h => h.Task.ProjectId == projectId)
                .Where(h => h.CreatedAt >= dateFrom && h.CreatedAt <= dateTo)
                .Include(h => h.Column)
                .OrderBy(h => h.CreatedAt)
                .ToListAsync();

            var result = new List<CumulativeFlowDataPointDto>();

            for (var date = dateFrom.Date; date <= dateTo.Date; date = date.AddDays(1))
            {
                // Minden taskra az adott napon érvényes utolsó státusz
                var taskStatuses = histories
                    .Where(h => h.CreatedAt.Date <= date)
                    .GroupBy(h => h.TaskId)
                    .Select(g => g.OrderByDescending(h => h.CreatedAt).First())
                    .ToList();

                var statusCounts = statuses.Select(status => new StatusCountDto
                {
                    Status = status,
                    Count = taskStatuses.Count(h =>
                        (h.Column?.MapsToStatus ?? "Backlog") == status)
                }).ToList();

                result.Add(new CumulativeFlowDataPointDto
                {
                    Date = date,
                    StatusCounts = statusCounts
                });
            }

            return result;
        }

        public async Task<List<TaskStatusDistributionDto>> GetTaskStatusDistributionAsync(Guid projectId, Guid? sprintId = null)
        {
            var query = _context.ProjectTasks
                .Where(t => t.ProjectId == projectId)
                .Where(t => sprintId == null || t.SprintId == sprintId)
                .Include(t => t.ColumnDefinition);

            var tasks = await query.ToListAsync();

            return tasks
                .GroupBy(t => t.ColumnDefinition?.MapsToStatus ?? "Backlog")
                .Select(g => new TaskStatusDistributionDto
                {
                    Status = g.Key,
                    Count = g.Count()
                })
                .ToList();
        }

        public async Task<List<VelocityDataPointDto>> GetVelocityAsync(Guid projectId)
        {
            var completedSprints = await _context.Sprints
                .Where(s => s.ProjectId == projectId && s.State == "Completed")
                .OrderBy(s => s.EndDate)
                .ToListAsync();

            var result = new List<VelocityDataPointDto>();

            foreach (var sprint in completedSprints)
            {
                var completedTasks = await _context.ProjectTasks
                    .CountAsync(t => t.SprintId == sprint.Id && t.CompletedAt != null);

                result.Add(new VelocityDataPointDto
                {
                    SprintName = sprint.Name,
                    CompletedTasks = completedTasks,
                    SprintEndDate = sprint.EndDate
                });
            }

            return result;
        }

        public async Task<List<WorkloadDataPointDto>> GetWorkloadAsync(Guid projectId, Guid? sprintId = null)
        {
            var query = _context.TaskAssignments
                .Where(ta => ta.ProjectTask.ProjectId == projectId)
                .Where(ta => ta.ProjectTask.ClosedAt == null)
                .Where(ta => sprintId == null || ta.ProjectTask.SprintId == sprintId)
                .Include(ta => ta.User)
                .Include(ta => ta.ProjectTask);

            var assignments = await query.ToListAsync();

            return assignments
                .GroupBy(ta => ta.User.DisplayName)
                .Select(g => new WorkloadDataPointDto
                {
                    UserName = g.Key,
                    TaskCount = g.Count()
                })
                .OrderByDescending(w => w.TaskCount)
                .ToList();
        }
    }
}
