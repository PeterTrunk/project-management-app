using Microsoft.EntityFrameworkCore;
using ProjectManager.API.DTOs.ProjectTask;
using ProjectManager.API.Model;
using ProjectManager.IntegrationTests.Infrastructure;

namespace ProjectManager.IntegrationTests.Concurrency
{
    /// <summary>
    /// A duplikált sorrendkulcs felismerése és javítása.
    /// </summary>
    /// <remarks>
    /// <b>Miért van ez a tesztosztály.</b> A mozgatásban volt egy újrasorszámozást kiváltó
    /// ág, amely azonos pozíciójú szomszédokra figyelt - és <b>soha nem futott le</b>. A
    /// szomszédot szigorúan nagyobb pozícióra szűrtük, miközben a vizsgálat egyenlőséget
    /// keresett: a két feltétel kizárta egymást. A hiba csendes volt, mert a hiányából nem
    /// keletkezett hibaüzenet, csak egy soha be nem következő javítás.
    ///
    /// Ez a tesztosztály a javítás UTÁNI állapotot rögzíti, két felismerési módra:
    /// <list type="bullet">
    /// <item>horgonnyal ("X után"): a horgony ikerpárja mostantól megtalálható,</item>
    /// <item>horgony nélkül (az oszlop elejére): az első két feladat egyenlősége jelzi.</item>
    /// </list>
    ///
    /// <b>Ez javítás, nem megelőzés.</b> A duplikációt egy valódi versenyhelyzet hozza létre
    /// (két egyidejű mozgatás ugyanabba a résbe - lásd <c>ParallelWriteTests</c>), és azt ez
    /// a mechanizmus nem előzi meg. Amit megad: a duplikáció nem él tovább korlátlanul.
    ///
    /// A duplikált állapotot nyers SQL-lel állítjuk elő, mert a szolgáltatáson keresztül csak
    /// versenyhelyzetben keletkezhet - és egy versenyhelyzetre épülő előkészítés nem
    /// determinisztikus.
    /// </remarks>
    public class DuplicatePositionRepairTests(PostgresFixture fixture) : DatabaseTestBase(fixture)
    {
        [Fact]
        public async Task MoveTask_AHorgonyIkerparjaEsetén_UjrasorszamozassalJavit()
        {
            // EZ A LÉNYEGI ÁLLÍTÁS a 7. találatra: a korábban halott ág most él.
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "DPA");
            var (twinA, twinB, mover) = await ArrangeTwinsAsync(seed, "0|f00000");

            await using var act = CreateContext();
            var (sut, _) = ServiceFactory.CreateTaskService(act, seed.Owner.Id);
            var token = await ReadRowVersionAsync(mover.Id);

            //A kliens az egyik iker UTÁN akar beszúrni - vagyis a két iker közé.
            var result = await sut.MoveTaskAsync(seed.Project.Id, mover.Id, new MoveTaskDto
            {
                ColumnId = seed.Column.Id,
                AfterTaskId = twinA.Id,
                RowVersion = token
            });

            //Az újrasorszámozás lefutott: a bucket a következőre váltott (0 -> 1), és minden
            //pozíció a rövid, újrakiosztott hosszra állt.
            Assert.StartsWith("1|", result.Position);

            await using var verify = CreateContext();
            var positions = await verify.ProjectTasks.AsNoTracking()
                .Where(t => t.ColumnId == seed.Column.Id)
                .Select(t => t.Position)
                .ToListAsync();

            //A LÉNYEG: a duplikáció megszűnt, minden kulcs egyedi az oszlopban.
            Assert.Equal(positions.Count, positions.Distinct().Count());
            Assert.All(positions, p => Assert.StartsWith("1|", p));
        }

        [Fact]
        public async Task MoveTask_AzOszlopElejenLevoIkerparEsetén_UjrasorszamozassalJavit()
        {
            // A felismerés MÁSIK módja. Horgony nélkül nincs mihez hasonlítani, ezért a
            // lekérdezés az első KETTŐ feladatot hozza, és azok egyenlősége jelzi a hibát.
            //
            // Enélkül ez az eset soha nem javulna: az elejére szúrás mindig MINDKETTŐ elé
            // kerül, tehát a saját műveletünk helyes lenne, és nem keletkezne jelzés.
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "DPF");

            //Az ikerpár az oszlop ELEJÉN van: a seed feladata (0|i) mögöttük.
            var (_, _, mover) = await ArrangeTwinsAsync(seed, "0|100000");

            await using var act = CreateContext();
            var (sut, _) = ServiceFactory.CreateTaskService(act, seed.Owner.Id);
            var token = await ReadRowVersionAsync(mover.Id);

            //AfterTaskId nélkül: az oszlop elejére.
            var result = await sut.MoveTaskAsync(seed.Project.Id, mover.Id, new MoveTaskDto
            {
                ColumnId = seed.Column.Id,
                AfterTaskId = null,
                RowVersion = token
            });

            Assert.StartsWith("1|", result.Position);

            await using var verify = CreateContext();
            var positions = await verify.ProjectTasks.AsNoTracking()
                .Where(t => t.ColumnId == seed.Column.Id)
                .Select(t => t.Position)
                .ToListAsync();

            Assert.Equal(positions.Count, positions.Distinct().Count());
        }

        [Fact]
        public async Task MoveTask_EgyediKulcsokMellett_NEM_Ujrasorszamoz()
        {
            // A MÁSIK FELE, és ez a fontosabb a LexoRank célja szempontjából: a felismerés
            // nem lett túl bőkezű. Egyedi kulcsok mellett a mozgatás továbbra is EGY sort ír,
            // nem futtat N soros újrasorszámozást - különben a sorrendkezelés egész értelme
            // elvesznék, és ugyanott lennénk, mint egy egész számos megoldással.
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "DPN");

            await using var arrange = CreateContext();
            var first = Add(arrange, seed, "Elso", "0|c00000");
            var last = Add(arrange, seed, "Utolso", "0|y00000");
            var mover = Add(arrange, seed, "Mozgatando", "0|z00000");
            await arrange.SaveChangesAsync();

            await using var act = CreateContext();
            var (sut, _) = ServiceFactory.CreateTaskService(act, seed.Owner.Id);
            var token = await ReadRowVersionAsync(mover.Id);

            var result = await sut.MoveTaskAsync(seed.Project.Id, mover.Id, new MoveTaskDto
            {
                ColumnId = seed.Column.Id,
                AfterTaskId = first.Id,
                RowVersion = token
            });

            //A bucket NEM váltott: nem futott újrasorszámozás.
            Assert.StartsWith("0|", result.Position);

            await using var verify = CreateContext();
            var untouched = await verify.ProjectTasks.AsNoTracking()
                .Where(t => t.Id == first.Id || t.Id == last.Id)
                .ToListAsync();

            //És a szomszédok pozíciója bitre változatlan - egyetlen sort írtunk.
            Assert.Contains(untouched, t => t.Id == first.Id && t.Position == "0|c00000");
            Assert.Contains(untouched, t => t.Id == last.Id && t.Position == "0|y00000");
        }

        /// <summary>
        /// Két azonos pozíciójú feladat, plusz egy harmadik, amit mozgatni fogunk.
        /// </summary>
        private async Task<(ProjectTask TwinA, ProjectTask TwinB, ProjectTask Mover)>
            ArrangeTwinsAsync(SeededProject seed, string twinPosition)
        {
            await using var ctx = CreateContext();

            var twinA = Add(ctx, seed, "Iker A", twinPosition);
            var twinB = Add(ctx, seed, "Iker B", "0|zzzzz0");   //átmeneti, hogy ne ütközzön
            var mover = Add(ctx, seed, "Mozgatando", "0|zzzzz1");
            await ctx.SaveChangesAsync();

            //A duplikációt nyers SQL-lel állítjuk be: a szolgáltatás szándékosan nem engedi.
            await ctx.Database.ExecuteSqlRawAsync(
                """UPDATE "ProjectTasks" SET "Position" = {0} WHERE "Id" = {1}""",
                twinPosition, twinB.Id);

            return (twinA, twinB, mover);
        }

        private static ProjectTask Add(
            Microsoft.EntityFrameworkCore.DbContext ctx, SeededProject seed, string title, string pos)
        {
            var task = new ProjectTask
            {
                ProjectId = seed.Project.Id,
                BoardId = seed.Board.Id,
                ColumnId = seed.Column.Id,
                TaskKey = $"{seed.Project.ProjKey}-{Guid.NewGuid().ToString()[..6]}",
                Title = title,
                Position = pos,
                CreatedById = seed.Owner.Id
            };
            ctx.Add(task);
            return task;
        }

        private async Task<uint> ReadRowVersionAsync(Guid taskId)
        {
            await using var ctx = CreateContext();
            return (await ctx.ProjectTasks.AsNoTracking().FirstAsync(t => t.Id == taskId)).xmin;
        }
    }
}
