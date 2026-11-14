using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Common.Exceptions;
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
                throw new NotFoundException("Sprint nem található!");

            var startDate = DateTime.SpecifyKind(
                sprint.StartDate ?? DateTime.UtcNow.AddDays(-14), DateTimeKind.Utc);
            var endDate = DateTime.SpecifyKind(
                sprint.EndDate ?? DateTime.UtcNow, DateTimeKind.Utc);

            //CSAK a lezárási időpont kell, nem a teljes feladat.
            var completedAt = await _context.ProjectTasks
                .Where(t => t.SprintId == sprintId)
                .Select(t => t.CompletedAt)
                .ToListAsync();

            var totalTasks = completedAt.Count;

            //Rendezett lezárási napok, hogy a napi ciklus EGYSZER lépkedjen végig rajtuk.
            var completionDays = completedAt
                .Where(d => d.HasValue)
                .Select(d => d!.Value.Date)
                .OrderBy(d => d)
                .ToList();

            var result = new List<BurndownDataPointDto>();
            var completedByDate = 0;

            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                while (completedByDate < completionDays.Count
                       && completionDays[completedByDate] <= date)
                {
                    completedByDate++;
                }

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

        public async Task<List<CumulativeFlowDataPointDto>> GetCumulativeFlowAsync(
            Guid projectId, 
            DateTime dateFrom, 
            DateTime dateTo,
            Guid? boardId = null)
        {
            dateFrom = DateTime.SpecifyKind(dateFrom, DateTimeKind.Utc);
            dateTo = DateTime.SpecifyKind(dateTo, DateTimeKind.Utc).AddDays(1).AddSeconds(-1);

            //Csak MapsToStatus kell. A 0. pozíció a Backlog oszlop, azt külön vesszük hozzá.
            //
            //A Distinct a hozzáfűzés UTÁN áll: a MapsToStatus szabad szöveg (a validátor csak hosszat ellenőriz),
            //tehát egy 0-nál nagyobb pozíciójú oszlop is kaphatja a "Backlog" értéket.
            //Enélkül a lenti ToDictionary kettős kulcsra futna, és a végpont az adott projektre tartósan hibát adna.
            var statuses = await _context.ColumnDefinitions
                .Where(c => c.Board.ProjectId == projectId &&
                            c.Position > 0 &&
                            !c.IsDeleted &&
                            (boardId == null || c.BoardId == boardId))
                .Select(c => c.MapsToStatus)
                .Distinct()
                .ToListAsync();

            statuses.Add("Backlog");
            statuses = statuses.Distinct().ToList();

            //A NYITÓÁLLAPOT: feladatonként az utolsó állapot az időszak KEZDETE előtt.
            //
            //Enélkül az ábra néma hibát ad. A korábbi megoldás csak az ablakon belüli sorokat töltötte be,
            //és minden feladatot ezekből számolt - vagyis amelyik feladat az időszakban nem mozdult,
            //az egyetlen napon sem került bele.
            //A felület alapértelmezése 30 nap: egy régebbi projekten az állandó,
            //nem mozgatott munka pont a torlódást mutató sávokból hiányzott volna.
            var carryIn = await _context.ProjectTasks
                .Where(t => t.ProjectId == projectId)
                .Select(t => new
                {
                    TaskId = t.Id,
                    Status = t.StatusHistory
                        .Where(h => h.CreatedAt < dateFrom)
                        .Where(h => boardId == null ||
                                    (h.Column != null && h.Column.BoardId == boardId))
                        .OrderByDescending(h => h.CreatedAt)
                        .Select(h => h.Column != null ? h.Column.MapsToStatus : "Backlog")
                        .FirstOrDefault()
                })
                .ToListAsync();

            //Csak TaskId, CreatedAt, MapsToStatus kell
            var histories = await _context.TaskStatusHistories
                .Where(h => h.Task.ProjectId == projectId)
                .Where(h => h.CreatedAt >= dateFrom && h.CreatedAt <= dateTo)
                .Where(h => boardId == null ||
                            (h.Column != null && h.Column.BoardId == boardId))
                .Select(h => new
                {
                    h.TaskId,
                    h.CreatedAt,
                    Status = h.Column != null ? h.Column.MapsToStatus : "Backlog"
                })
                .ToListAsync();

            //Az ablakon belüli átlépések naponként, időrendben.
            var transitionsByDay = histories
                .OrderBy(h => h.CreatedAt)
                .GroupBy(h => h.CreatedAt.Date)
                .ToDictionary(g => g.Key, g => g.ToList());

            //Feladatonkénti FOLYÓ állapot, a nyitóállapotból indulva.
            var currentStatus = new Dictionary<Guid, string>();
            foreach (var task in carryIn)
            {
                if (task.Status != null)
                    currentStatus[task.TaskId] = task.Status;
            }

            var statusCounts = statuses.ToDictionary(s => s, s => 0);
            foreach (var status in currentStatus.Values)
            {
                if (statusCounts.ContainsKey(status))
                    statusCounts[status]++;
            }

            var result = new List<CumulativeFlowDataPointDto>();

            //Napi KÜLÖNBSÉGEK alkalmazása: a feladat kilép a korábbi állapotból, belép az újba.
            //A korábbi megoldás minden napra újraolvasta minden feladat teljes történetét.
            //Így a munka az átlépések számával EGYSZER arányos.
            for (var date = dateFrom.Date; date <= dateTo.Date; date = date.AddDays(1))
            {
                if (transitionsByDay.TryGetValue(date, out var transitions))
                {
                    foreach (var transition in transitions)
                    {
                        if (currentStatus.TryGetValue(transition.TaskId, out var previous)
                            && statusCounts.ContainsKey(previous))
                        {
                            statusCounts[previous]--;
                        }

                        currentStatus[transition.TaskId] = transition.Status;

                        if (statusCounts.ContainsKey(transition.Status))
                            statusCounts[transition.Status]++;
                    }
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
            //Az összesítés az ADATBÁZISBAN marad. Korábban két Include töltötte be a teljes
            //felhasználói és feladatsorokat, hogy azokat a memóriában csoportosítsuk,
            //egy felhasználónkénti darabszámhoz.
            return await _context.TaskAssignments
                .Where(ta => ta.ProjectTask.ProjectId == projectId)
                .Where(ta => ta.ProjectTask.ClosedAt == null)
                .Where(ta => sprintId == null || ta.ProjectTask.SprintId == sprintId)
                .GroupBy(ta => new { ta.UserId, ta.User.DisplayName })
                .Select(g => new WorkloadDataPointDto
                {
                    UserName = g.Key.DisplayName,
                    TaskCount = g.Count()
                })
                .OrderByDescending(w => w.TaskCount)
                .ToListAsync();
        }
    }
}
