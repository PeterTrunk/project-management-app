using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Data;
using ProjectManager.API.Model;
using ProjectManager.API.Services.StatisticsService;
using ProjectManager.IntegrationTests.Infrastructure;

namespace ProjectManager.IntegrationTests.Statistics
{
    /// <summary>
    /// A kimutatások lekérdezései.
    ///
    /// Ezek a tesztek nem a diagramokat ellenőrzik, hanem azt a három állítást, amelyen a
    /// kimutatások helyessége múlik: hogy az időszak előtt megállt munka is látszik, hogy a
    /// szabad szöveges állapotnév nem tudja elhasítani a lekérdezést, és hogy az összesítés
    /// a megfelelő kulcs szerint csoportosít.
    /// </summary>
    public class StatisticsQueryTests : DatabaseTestBase
    {
        public StatisticsQueryTests(PostgresFixture fixture) : base(fixture) { }

        private static StatisticsService Sut(AppDbContext context) => new(context);

        /// <summary>Egy 0-nál nagyobb pozíciójú oszlop a megadott állapotnévvel.</summary>
        private static async Task<ColumnDefinition> AddColumnAsync(
            AppDbContext context, Guid boardId, string mapsToStatus, int position)
        {
            var column = new ColumnDefinition
            {
                BoardId = boardId,
                Name = mapsToStatus,
                MapsToStatus = mapsToStatus,
                Position = position
            };

            context.ColumnDefinitions.Add(column);
            await context.SaveChangesAsync();

            return column;
        }

        private static async Task AddHistoryAsync(
            AppDbContext context, Guid taskId, Guid? columnId, DateTime createdAt)
        {
            context.TaskStatusHistories.Add(new TaskStatusHistory
            {
                TaskId = taskId,
                ColumnId = columnId,
                CreatedAt = DateTime.SpecifyKind(createdAt, DateTimeKind.Utc)
            });

            await context.SaveChangesAsync();
        }

        //A kumulatív folyamatábra

        /// <summary>
        /// A lényegi eset: a feladat az időszak ELŐTT lépett az állapotába, és azóta nem
        /// mozdult. A korábbi megoldás csak az ablakon belüli státuszsorokat töltötte be,
        /// ezért az ilyen feladat egyetlen napon sem számolódott - vagyis a torlódást mutató
        /// sáv pont az állandó munkát hagyta ki.
        /// </summary>
        [Fact]
        public async Task CumulativeFlow_TaskThatStoppedBeforeTheWindow_IsStillCounted()
        {
            await using var context = CreateContext();
            var seeded = await TestData.SeedProjectAsync(context, "CFD");
            var inProgress = await AddColumnAsync(context, seeded.Board.Id, "In Progress", 1);

            //Az egyetlen állapotváltozás két hónapja volt
            await AddHistoryAsync(context, seeded.Task.Id, inProgress.Id,
                DateTime.UtcNow.AddDays(-60));

            var from = DateTime.UtcNow.AddDays(-7);
            var to = DateTime.UtcNow;

            var result = await Sut(context).GetCumulativeFlowAsync(seeded.Project.Id, from, to);

            Assert.NotEmpty(result);
            Assert.All(result, point =>
                Assert.Equal(1, point.StatusCounts.Single(s => s.Status == "In Progress").Count));
        }

        /// <summary>
        /// Az ablakon belüli átlépés a megfelelő naptól számít, és a korábbi állapotból
        /// kilép - a két sáv összege nem nőhet a feladatok számánál nagyobbra.
        /// </summary>
        [Fact]
        public async Task CumulativeFlow_TransitionInsideTheWindow_MovesTheTaskBetweenBands()
        {
            await using var context = CreateContext();
            var seeded = await TestData.SeedProjectAsync(context, "CFM");
            var todo = await AddColumnAsync(context, seeded.Board.Id, "To Do", 1);
            var done = await AddColumnAsync(context, seeded.Board.Id, "Done", 2);

            await AddHistoryAsync(context, seeded.Task.Id, todo.Id, DateTime.UtcNow.AddDays(-30));
            await AddHistoryAsync(context, seeded.Task.Id, done.Id, DateTime.UtcNow.AddDays(-2));

            var result = await Sut(context).GetCumulativeFlowAsync(
                seeded.Project.Id, DateTime.UtcNow.AddDays(-5), DateTime.UtcNow);

            var first = result.First();
            var last = result.Last();

            Assert.Equal(1, first.StatusCounts.Single(s => s.Status == "To Do").Count);
            Assert.Equal(0, first.StatusCounts.Single(s => s.Status == "Done").Count);

            Assert.Equal(0, last.StatusCounts.Single(s => s.Status == "To Do").Count);
            Assert.Equal(1, last.StatusCounts.Single(s => s.Status == "Done").Count);

            //Egyetlen feladat van: egyszerre csak egy sávban állhat
            Assert.All(result, point => Assert.Equal(1, point.StatusCounts.Sum(s => s.Count)));
        }

        /// <summary>
        /// A MapsToStatus szabad szöveg: a validátor csak hosszat ellenőriz. Egy "Backlog"
        /// nevű állapot egy 0-nál nagyobb pozíciójú oszlopon ezért létrehozható - és a
        /// lekérdezés a beépített "Backlog" mellé téve kettős kulcsot kapna.
        /// </summary>
        [Fact]
        public async Task CumulativeFlow_ColumnNamedBacklog_DoesNotBreakTheQuery()
        {
            await using var context = CreateContext();
            var seeded = await TestData.SeedProjectAsync(context, "CFB");
            await AddColumnAsync(context, seeded.Board.Id, "Backlog", 1);

            var result = await Sut(context).GetCumulativeFlowAsync(
                seeded.Project.Id, DateTime.UtcNow.AddDays(-3), DateTime.UtcNow);

            Assert.NotEmpty(result);
            Assert.All(result, point =>
                Assert.Single(point.StatusCounts, s => s.Status == "Backlog"));
        }

        //A csapatterhelés

        /// <summary>
        /// A csoportosítás kulcsa a felhasználó azonosítója is, nem csak a megjelenítési
        /// neve: két azonos nevű tag egyébként egyetlen oszlopba olvadna össze.
        /// </summary>
        [Fact]
        public async Task Workload_TwoMembersWithTheSameDisplayName_StayTwoRows()
        {
            await using var context = CreateContext();
            var seeded = await TestData.SeedProjectAsync(context, "WL");

            var second = new User
            {
                Email = "masik@example.com",
                DisplayName = seeded.Owner.DisplayName,
                PasswordHash = "nem-valodi-hash",
                IsEmailVerified = true
            };
            context.Users.Add(second);

            context.TaskAssignments.Add(new TaskAssignment
            {
                TaskId = seeded.Task.Id,
                UserId = seeded.Owner.Id,
                AssignedAt = DateTime.UtcNow
            });
            context.TaskAssignments.Add(new TaskAssignment
            {
                TaskId = seeded.Task.Id,
                UserId = second.Id,
                AssignedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var result = await Sut(context).GetWorkloadAsync(seeded.Project.Id);

            Assert.Equal(2, result.Count);
            Assert.All(result, row => Assert.Equal(1, row.TaskCount));
        }

        /// <summary>A lezárt feladat hozzárendelése nem terheli tovább a tagot.</summary>
        [Fact]
        public async Task Workload_ClosedTask_IsNotCounted()
        {
            await using var context = CreateContext();
            var seeded = await TestData.SeedProjectAsync(context, "WLC");

            context.TaskAssignments.Add(new TaskAssignment
            {
                TaskId = seeded.Task.Id,
                UserId = seeded.Owner.Id,
                AssignedAt = DateTime.UtcNow
            });

            var task = await context.ProjectTasks.SingleAsync(t => t.Id == seeded.Task.Id);
            task.ClosedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();

            Assert.Empty(await Sut(context).GetWorkloadAsync(seeded.Project.Id));
        }

        //A burndown

        /// <summary>
        /// A lezárás KUMULATÍV: ami egyszer elkészült, az a következő napokon is az marad.
        /// A napi ciklus ezért egyszer lépked végig a rendezett lezárási napokon.
        /// </summary>
        [Fact]
        public async Task Burndown_CompletedTasks_AccumulateOverTheSprint()
        {
            await using var context = CreateContext();
            var seeded = await TestData.SeedProjectAsync(context, "BD");

            var sprint = await context.Sprints.SingleAsync(s => s.Id == seeded.Sprint.Id);
            sprint.StartDate = DateTime.UtcNow.Date.AddDays(-4);
            sprint.EndDate = DateTime.UtcNow.Date;

            var first = await context.ProjectTasks.SingleAsync(t => t.Id == seeded.Task.Id);
            first.SprintId = sprint.Id;
            first.CompletedAt = DateTime.UtcNow.Date.AddDays(-3);

            context.ProjectTasks.Add(new ProjectTask
            {
                ProjectId = seeded.Project.Id,
                BoardId = seeded.Board.Id,
                ColumnId = seeded.Column.Id,
                CreatedById = seeded.Owner.Id,
                SprintId = sprint.Id,
                TaskKey = "BD-2",
                Title = "Második",
                Position = "0|hzzzzz:",
                CompletedAt = DateTime.UtcNow.Date.AddDays(-1)
            });
            await context.SaveChangesAsync();

            var result = await Sut(context).GetBurndownAsync(seeded.Project.Id, sprint.Id);

            Assert.All(result, point => Assert.Equal(2, point.TotalTasks));
            Assert.Equal(0, result.First().CompletedTasks);
            Assert.Equal(2, result.Last().CompletedTasks);

            //Soha nem csökkenhet
            var counts = result.Select(p => p.CompletedTasks).ToList();
            Assert.Equal(counts.OrderBy(c => c), counts);

            //A hátralévő és az elkészült mindig kiadja a teljeset
            Assert.All(result, p => Assert.Equal(p.TotalTasks, p.CompletedTasks + p.RemainingTasks));
        }
    }
}
