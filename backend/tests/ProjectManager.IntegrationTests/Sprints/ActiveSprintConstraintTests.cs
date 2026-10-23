using Microsoft.EntityFrameworkCore;
using Npgsql;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.Model;
using ProjectManager.IntegrationTests.Infrastructure;

namespace ProjectManager.IntegrationTests.Sprints
{
    /// <summary>
    /// A „projektenként egy aktív sprint" kényszer tesztjei.
    ///
    /// Azért integrációs tesztek, mert az állítás **az adatbázisról** szól, nem a kódról:
    /// egy szűrt (parciális) egyedi index helyességét csak valódi PostgreSQL ellen lehet
    /// mérni. Az EF Core InMemory providere a szűrt indexet nem is tartatja be.
    ///
    /// Miért van rájuk szükség: a kényszer **2026 márciusa óta néma volt**. A szűrő
    /// `'active'`-ot tartalmazott kisbetűvel, az alkalmazás viszont `'Active'`-ot ír, ezért az
    /// index egyetlen sorra sem illeszkedett. A hiba azért élhetett hónapokig, mert egy
    /// *feltételes* kényszer sosem jelez, ha a feltétel nem illeszkedik - szemben egy sima
    /// `UNIQUE`-kal, ami az első használatnál kiderül.
    ///
    /// Ez a tesztosztály tehát nem a funkciót védi, hanem **a védelem működését**.
    /// </summary>
    public class ActiveSprintConstraintTests(PostgresFixture fixture) : DatabaseTestBase(fixture)
    {
        [Fact]
        public async Task TheIndexFilter_MatchesTheValueTheApplicationWrites()
        {
            //A lényegi állítás, és szándékosan a sémából olvasva: a szűrő szövegének azt az
            //értéket kell tartalmaznia, amit a SprintStates.Active konstans ad. Ha valaki a
            //konstanst átírja és a szűrőt nem (vagy fordítva), ez a teszt bukik el - nem
            //pedig hónapokkal később derül ki, hogy a kényszer hatástalan.
            await using var context = CreateContext();

            var filter = await context.Database
                .SqlQuery<string?>($"""
                    SELECT pg_get_expr(i.indpred, i.indrelid) AS "Value"
                    FROM pg_index i
                    JOIN pg_class c ON c.oid = i.indexrelid
                    WHERE c.relname = 'IX_Sprints_OneActiveSprint'
                    """)
                .SingleAsync();

            Assert.NotNull(filter);
            Assert.Contains($"'{SprintStates.Active}'", filter);
        }

        [Fact]
        public async Task TwoActiveSprints_InTheSameProject_AreRejectedByTheDatabase()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "ONE");

            //A seedelt sprint állapotát ismerjük, ezért explicit állítjuk be
            seed.Sprint.State = SprintStates.Active;
            await context.SaveChangesAsync();

            context.Sprints.Add(new Sprint
            {
                ProjectId = seed.Project.Id,
                Name = "Második aktív",
                State = SprintStates.Active
            });

            var exception = await Assert.ThrowsAsync<DbUpdateException>(
                () => context.SaveChangesAsync());

            //23505 = unique_violation. A kódot a láncból keressük: az EF a PostgreSQL
            //hibáját becsomagolja, tehát a legkülső kivételen nem látszik.
            var postgres = FindPostgresException(exception);
            Assert.NotNull(postgres);
            Assert.Equal("23505", postgres!.SqlState);
        }

        [Fact]
        public async Task TwoActiveSprints_InDifferentProjects_AreAllowed()
        {
            //A kényszer PROJEKTENKÉNT egy aktív sprintet engedélyez, nem rendszerszinten
            //egyet. Ez a teszt azt zárja ki, hogy a javítás túl szigorúra sikerült.
            await using var context = CreateContext();
            var a = await TestData.SeedProjectAsync(context, "AAA");
            var b = await TestData.SeedProjectAsync(context, "BBB");

            a.Sprint.State = SprintStates.Active;
            b.Sprint.State = SprintStates.Active;

            await context.SaveChangesAsync();

            await using var verify = CreateContext();
            var activeCount = await verify.Sprints.CountAsync(s => s.State == SprintStates.Active);
            Assert.Equal(2, activeCount);
        }

        [Fact]
        public async Task AnActiveAndACompletedSprint_InTheSameProject_AreAllowed()
        {
            //A szűrt index csak az aktív sprinteket nézi: egy projektben több lezárt sprint
            //mellett lehet egy aktív. Ha a szűrő elveszne (és az index teljes egyedivé
            //válna), ez a teszt bukna el.
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "MIX");

            seed.Sprint.State = SprintStates.Active;
            context.Sprints.Add(new Sprint
            {
                ProjectId = seed.Project.Id,
                Name = "Lezárt",
                State = SprintStates.Completed
            });
            context.Sprints.Add(new Sprint
            {
                ProjectId = seed.Project.Id,
                Name = "Tervezett",
                State = SprintStates.Planning
            });

            await context.SaveChangesAsync();

            await using var verify = CreateContext();
            Assert.Equal(3, await verify.Sprints.CountAsync(s => s.ProjectId == seed.Project.Id));
        }

        private static PostgresException? FindPostgresException(Exception exception)
        {
            for (var current = exception; current != null; current = current.InnerException)
            {
                if (current is PostgresException postgres)
                    return postgres;
            }

            return null;
        }
    }
}
