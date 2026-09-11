using Microsoft.EntityFrameworkCore;
using ProjectManager.IntegrationTests.Infrastructure;

namespace ProjectManager.IntegrationTests
{
    /// <summary>
    /// Az infrastruktúra füstpróbája: azt igazolja, hogy a konténer, a migrációk, 
    /// a Respawn és a PostgreSQL-specifikus leképzések tényleg működnek. 
    /// Ha ezek elbuknak, minden más teszt megtévesztő vagy hibás lenne.
    /// </summary>
    public class DatabaseSmokeTests : DatabaseTestBase
    {
        public DatabaseSmokeTests(PostgresFixture fixture) : base(fixture) { }

        [Fact]
        public async Task Migrations_HaveBeenApplied()
        {
            await using var context = CreateContext();

            var applied = await context.Database.GetAppliedMigrationsAsync();

            Assert.NotEmpty(applied);
            Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        }

        [Fact]
        public async Task SeededGraph_IsReadableFromAnotherContext()
        {
            await using (var seedContext = CreateContext())
            {
                await TestData.SeedProjectAsync(seedContext, "SMK");
            }

            //FRISS context: a change tracker ne hazudhassa, hogy megvan a sor
            await using var verify = CreateContext();

            var task = await verify.ProjectTasks
                .Include(t => t.Project)
                .SingleAsync();

            Assert.Equal("SMK-1", task.TaskKey);
            Assert.Equal("SMK", task.Project.ProjKey);
        }

        //Az AppDbContext a CreatedAt/UpdatedAt mezőket a SaveChangesAsync felülírásában állítja be triggerek helyett.
        //Ha ez elromlik, minden entitáson csendben nulla dátum marad.
        [Fact]
        public async Task Timestamps_AreStampedOnInsert()
        {
            await using (var seedContext = CreateContext())
            {
                await TestData.SeedProjectAsync(seedContext, "STM");
            }

            await using var verify = CreateContext();
            var task = await verify.ProjectTasks.SingleAsync();

            Assert.NotEqual(default, task.CreatedAt);
            Assert.NotEqual(default, task.UpdatedAt);
            Assert.Equal(DateTimeKind.Utc, task.CreatedAt.Kind);
        }

        //Az xmin a PostgreSQL rendszeroszlopa, amit az EF konkurenciavezérlésre használ.
        //Az InMemory provider ezt egyáltalán nem tudná, ez az egyik oka a valódi adatbázisnak.
        [Fact]
        public async Task Xmin_IsPopulatedAndChangesOnUpdate()
        {
            Guid taskId;

            await using (var seedContext = CreateContext())
            {
                var seed = await TestData.SeedProjectAsync(seedContext, "XMN");
                taskId = seed.Task.Id;
            }

            uint versionAfterInsert;
            await using (var read = CreateContext())
            {
                var task = await read.ProjectTasks.SingleAsync(t => t.Id == taskId);
                versionAfterInsert = task.xmin;
                Assert.NotEqual(0u, versionAfterInsert);
            }

            await using (var update = CreateContext())
            {
                var task = await update.ProjectTasks.SingleAsync(t => t.Id == taskId);
                task.Title = "Módosított cím";
                await update.SaveChangesAsync();
            }

            await using var verify = CreateContext();
            var updated = await verify.ProjectTasks.SingleAsync(t => t.Id == taskId);

            Assert.NotEqual(versionAfterInsert, updated.xmin);
        }

        //A Respawn minden teszt ELŐTT ürít.
        //Ez a teszt azt igazolja, hogy tényleg tisztán indulunk,
        //A fenti tesztek mind seedelnek, mégsem látjuk a soraikat.
        [Fact]
        public async Task Respawn_LeavesAnEmptyDatabaseBeforeEachTest()
        {
            await using var context = CreateContext();

            Assert.Empty(await context.Projects.ToListAsync());
            Assert.Empty(await context.Users.ToListAsync());
            Assert.Empty(await context.ProjectTasks.ToListAsync());
        }

        //A szinkron SaveChanges() NINCS felülírva az AppDbContextben, csak az aszinkron változat.
        //Tehát szinkron mentésnél mind a 24 entitáson kimarad az időbélyegzés.
        //Ez viselkedésváltozás lenne minden entitáson.
        [Fact(Skip = "Ismert eltérés: a szinkron SaveChanges() nem bélyegez időt. Külön döntés, hogy javítjuk-e.")]
        public async Task SyncSaveChanges_AlsoStampsTimestamps()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "SYN");

            var board = new ProjectManager.API.Model.Board
            {
                ProjectId = seed.Project.Id,
                Name = "Szinkron mentéssel"
            };

            context.Boards.Add(board);
            context.SaveChanges();

            await using var verify = CreateContext();
            var saved = await verify.Boards.SingleAsync(b => b.Id == board.Id);

            Assert.NotEqual(default, saved.CreatedAt);
        }
    }
}
