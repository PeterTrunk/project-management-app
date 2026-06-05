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

            var startDate = DateTime.SpecifyKind(
                sprint.StartDate ?? DateTime.UtcNow.AddDays(-14), DateTimeKind.Utc);
            var endDate = DateTime.SpecifyKind(
                sprint.EndDate ?? DateTime.UtcNow, DateTimeKind.Utc);

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
            dateFrom = DateTime.SpecifyKind(dateFrom, DateTimeKind.Utc);
            dateTo = DateTime.SpecifyKind(dateTo, DateTimeKind.Utc).AddDays(1).AddSeconds(-1);

            //Csak MapsToStatus kell
            var statuses = await _context.ColumnDefinitions
                .Where(c => c.Board.ProjectId == projectId && c.Position > 0 && !c.IsDeleted)
                .Select(c => c.MapsToStatus)
                .Distinct()
                .ToListAsync();
            statuses.Add("Backlog");

            //Csak TaskId, CreatedAt, MapsToStatus kell
            var histories = await _context.TaskStatusHistories
                .Where(h => h.Task.ProjectId == projectId)
                .Where(h => h.CreatedAt >= dateFrom && h.CreatedAt <= dateTo)
                .Select(h => new
                {
                    h.TaskId,
                    h.CreatedAt,
                    Status = h.Column != null ? h.Column.MapsToStatus : "Backlog"
                })
                .OrderBy(h => h.CreatedAt)
                .ToListAsync();

            //A cikluson kívül csoportosítunk egyszer: O(histories)
            var taskHistories = histories
                .GroupBy(h => h.TaskId)
                .Select(g => g.OrderBy(h => h.CreatedAt).ToList())
                .ToList();

            var result = new List<CumulativeFlowDataPointDto>();

            for (var date = dateFrom.Date; date <= dateTo.Date; date = date.AddDays(1))
            {
                // Dictionary alapú számlálás – O(tasks) naponta, nem O(histories)
                var statusCounts = statuses.ToDictionary(s => s, s => 0);

                foreach (var taskHistory in taskHistories)
                {
                    var lastEntry = taskHistory.LastOrDefault(h => h.CreatedAt.Date <= date);
                    if (lastEntry != null && statusCounts.ContainsKey(lastEntry.Status))
                        statusCounts[lastEntry.Status]++;
                }

                result.Add(new CumulativeFlowDataPointDto
                {
                    Date = date,
                    StatusCounts = statuses.Select(s => new StatusCountDto
                    {
                        Status = s,
                        Count = statusCounts[s]
                    }).ToList()
                });
            }

            return result;
        }

        public async Task<List<TaskStatusDistributionDto>> GetTaskStatusDistributionAsync(Guid projectId, Guid? sprintId = null)
        {
            return await _context.ProjectTasks
                .Where(t => t.ProjectId == projectId)
                .Where(t => sprintId == null || t.SprintId == sprintId)
                .GroupBy(t => t.ColumnDefinition != null ? t.ColumnDefinition.MapsToStatus : "Backlog")
                .Select(g => new TaskStatusDistributionDto
                {
                    Status = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();
        }

        public async Task<List<VelocityDataPointDto>> GetVelocityAsync(Guid projectId)
        {
            return await _context.Sprints
                .Where(s => s.ProjectId == projectId && s.State == "Completed")
                .OrderBy(s => s.EndDate)
                .Select(s => new VelocityDataPointDto
                {
                    SprintName = s.Name,
                    SprintEndDate = s.EndDate,
                    CompletedTasks = s.ProjectTasks.Count(t => t.CompletedAt != null)
                })
                .ToListAsync();
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
