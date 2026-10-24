using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Common.Exceptions;
using ProjectManager.API.Services.CounterService;
using ProjectManager.IntegrationTests.Infrastructure;

namespace ProjectManager.IntegrationTests.Counters
{
    /// <summary>
    /// A feladatsorszám-számláló tesztjei.
    ///
    /// Miért most készültek el: a számláló eddig **teljesen fedetlen** volt. Egyetlen teszt
    /// sem hívta a `CreateTaskAsync`-et - a tesztadat a feladatokat közvetlenül szúrja be,
    /// a cross-project mátrix hálója pedig azért nem követelte meg, mert a metódus második
    /// paramétere DTO, nem `Guid` (ugyanaz a vakfolt, amit a projekt-hatókörnél is
    /// megtaláltunk). Vagyis a számláló helyességéről eddig nem volt mérés.
    ///
    /// A legfontosabb állítás a konkurencia-teszt: az invariáns, amit védeni kell, az, hogy
    /// **két feladat nem kaphatja ugyanazt a sorszámot**. Ez a sorok közötti állítás, amit az
    /// xmin konkurencia-token nem tud kikényszeríteni.
    ///
    /// A tesztek szándékosan a **pillanatnyi számlálóállásra nézve relatívak**, nem konkrét
    /// számokra. Ennek oka mérés: a tesztadat `LastNum = 1`-gyel indul, mert a seed létre is
    /// hoz egy feladatot `{projKey}-1` kulccsal - az éles projektlétrehozás viszont 0-ról
    /// indul. Egy nulla kiindulást feltételező állítás ezért a seed választását kötné le, nem
    /// a szolgáltatás szerződését. (Ez a tesztek első változatát el is buktatta.)
    /// </summary>
    public class TaskNumberCounterTests(PostgresFixture fixture) : DatabaseTestBase(fixture)
    {
        /// <summary>A számláló pillanatnyi állása az adatbázisból, friss contextből.</summary>
        private async Task<long> ReadCounterAsync(Guid projectId)
        {
            await using var verify = CreateContext();
            return await verify.ProjectCounters
                .Where(c => c.ProjectId == projectId)
                .Select(c => c.LastNum)
                .SingleAsync();
        }

        [Fact]
        public async Task GetNextTaskNumber_OnAFreshProject_StartsAtOne()
        {
            //Az éles projektlétrehozás 0-ra állítja a számlálót, ezért itt is azt állítjuk be:
            //ez a valódi első-feladat eset, és ennek pontosan 1-et kell adnia.
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "CNT");

            var counter = await context.ProjectCounters.SingleAsync(c => c.ProjectId == seed.Project.Id);
            counter.LastNum = 0;
            await context.SaveChangesAsync();

            var sut = new CounterService(context);
            var number = await sut.GetNextTaskNumberAsync(seed.Project.Id);

            Assert.Equal(1, number);
        }

        [Fact]
        public async Task GetNextTaskNumber_RepeatedCalls_NeverRepeatsANumber()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "CNT");
            var sut = new CounterService(context);

            var start = await ReadCounterAsync(seed.Project.Id);

            var numbers = new List<long>();
            for (var i = 0; i < 5; i++)
                numbers.Add(await sut.GetNextTaskNumberAsync(seed.Project.Id));

            Assert.Equal(Enumerable.Range(1, 5).Select(i => start + i), numbers);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(10)]
        public async Task GetNextTaskNumbers_Batch_ReturnsConsecutiveRange(int count)
        {
            //A kötegelt kiadás a sprint-lezárásnál és a többszörös létrehozásnál kell:
            //egyetlen növeléssel kell count darab sorszámot lefoglalni.
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "CNT");
            var sut = new CounterService(context);

            var start = await ReadCounterAsync(seed.Project.Id);

            var numbers = await sut.GetNextTaskNumbersAsync(seed.Project.Id, count);

            Assert.Equal(count, numbers.Count);
            Assert.Equal(Enumerable.Range(1, count).Select(i => start + i), numbers);
        }

        [Fact]
        public async Task GetNextTaskNumbers_AfterABatch_ContinuesWithoutAGap()
        {
            //A köteg utáni folytatás a legkönnyebben elhibázható rész: a visszaadott érték a
            //növelés UTÁNI utolsó sorszám, tehát a tartomány kezdete abból számolódik.
            //Egy elcsúszás itt vagy átlapoló, vagy kihagyott sorszámokat adna.
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "CNT");
            var sut = new CounterService(context);

            var start = await ReadCounterAsync(seed.Project.Id);

            var batch = await sut.GetNextTaskNumbersAsync(seed.Project.Id, 3);
            var next = await sut.GetNextTaskNumberAsync(seed.Project.Id);

            Assert.Equal(new[] { start + 1, start + 2, start + 3 }, batch);
            Assert.Equal(start + 4, next);
        }

        [Fact]
        public async Task GetNextTaskNumbers_TwoProjects_CountIndependently()
        {
            //A sorszám PROJEKTENKÉNT folytonos, nem rendszerszinten.
            await using var context = CreateContext();
            var a = await TestData.SeedProjectAsync(context, "AAA");
            var b = await TestData.SeedProjectAsync(context, "BBB");
            var sut = new CounterService(context);

            var startB = await ReadCounterAsync(b.Project.Id);

            await sut.GetNextTaskNumberAsync(a.Project.Id);
            await sut.GetNextTaskNumberAsync(a.Project.Id);
            var firstInB = await sut.GetNextTaskNumberAsync(b.Project.Id);

            //"B" számlálóját az "A" projektben kiadott két sorszám nem mozdította el
            Assert.Equal(startB + 1, firstInB);
        }

        [Fact]
        public async Task GetNextTaskNumber_UnknownProject_ThrowsNotFound()
        {
            //A számláló sort a projekt létrehozása hozza létre; ha hiányzik, az hiba, nem
            //pedig hallgatólagosan 1-től induló sorszámozás.
            await using var context = CreateContext();
            var sut = new CounterService(context);

            await Assert.ThrowsAsync<NotFoundException>(
                () => sut.GetNextTaskNumberAsync(Guid.NewGuid()));
        }

        [Fact]
        public async Task GetNextTaskNumbers_ZeroOrNegativeCount_ThrowsValidation()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "CNT");
            var sut = new CounterService(context);

            await Assert.ThrowsAsync<ValidationException>(
                () => sut.GetNextTaskNumbersAsync(seed.Project.Id, 0));
        }

        [Fact]
        public async Task GetNextTaskNumber_ConcurrentCallers_NeverHandOutTheSameNumber()
        {
            //EZ A LÉNYEGI ÁLLÍTÁS, és ez volt az eddig hiányzó mérés.
            //
            //Minden hívó SAJÁT contextet, tehát saját adatbázis-kapcsolatot kap - különben a
            //párhuzamosság látszólagos lenne, és a teszt nem mérne semmit.
            //
            //Az atomi `UPDATE … RETURNING` mellett mindegyik hívás sikeres, és a kiadott
            //sorszámok diszjunktak. A korábbi, Serializable tranzakciós megoldásnál a
            //szerializációs hiba is felléphetett volna, amit a retry-ág csak a nyers
            //PostgresException alakban fogott - épp ez volt az a méretlen rés, amit ez a
            //teszt most lefed.
            await using var seedContext = CreateContext();
            var seed = await TestData.SeedProjectAsync(seedContext, "CNT");
            var start = await ReadCounterAsync(seed.Project.Id);

            const int callers = 8;
            const int perCaller = 5;

            var contexts = Enumerable.Range(0, callers).Select(_ => CreateContext()).ToList();
            try
            {
                var tasks = contexts.Select(async ctx =>
                {
                    var sut = new CounterService(ctx);
                    var mine = new List<long>();

                    for (var i = 0; i < perCaller; i++)
                        mine.AddRange(await sut.GetNextTaskNumbersAsync(seed.Project.Id, 1));

                    return mine;
                });

                var results = await Task.WhenAll(tasks);
                var all = results.SelectMany(r => r).ToList();

                //Egyetlen sorszám sem ismétlődhet
                Assert.Equal(callers * perCaller, all.Count);
                Assert.Equal(all.Count, all.Distinct().Count());

                //És a kiadott halmaz hézagmentes: pontosan start+1 .. start+N
                Assert.Equal(
                    Enumerable.Range(1, callers * perCaller).Select(i => start + i),
                    all.OrderBy(x => x));
            }
            finally
            {
                foreach (var ctx in contexts)
                    await ctx.DisposeAsync();
            }
        }

        [Fact]
        public async Task TheCounterRow_IsAdvancedInTheDatabase_NotOnlyInMemory()
        {
            //A nyers SQL megkerüli a change trackert, ezért külön ellenőrizzük, hogy az
            //érték tényleg az adatbázisban nőtt - egy friss contextből visszaolvasva.
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "CNT");
            var sut = new CounterService(context);

            var start = await ReadCounterAsync(seed.Project.Id);

            await sut.GetNextTaskNumbersAsync(seed.Project.Id, 7);

            Assert.Equal(start + 7, await ReadCounterAsync(seed.Project.Id));
        }
    }
}
