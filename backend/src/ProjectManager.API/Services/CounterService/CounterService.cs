using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Common.Exceptions;
using ProjectManager.API.Data;

namespace ProjectManager.API.Services.CounterService
{
    public class CounterService : ICounterService
    {
        private readonly AppDbContext _context;

        public CounterService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<long> GetNextTaskNumberAsync(Guid projectId)
        {
            var numbers = await GetNextTaskNumbersAsync(projectId, 1);
            return numbers[0];
        }

        /// <summary>
        /// A következő <paramref name="count"/> darab projekten belüli feladatsorszám kiadása.
        /// </summary>
        /// <remarks>
        /// Az invariáns, amit védeni kell: két feladat nem kaphatja ugyanazt a sorszámot. Ez
        /// nem egy sor szándéka, hanem a sorok közötti állítás, ezért az xmin
        /// konkurencia-token itt nem eszköz.
        ///
        /// A megoldás EGYETLEN atomi utasítás: a növelés és az új érték visszaolvasása
        /// ugyanabban az `UPDATE`-ben történik, tehát két egyidejű hívás nem kaphatja
        /// ugyanazt a sorszámot - a PostgreSQL a soron belüli írásokat sorosítja.
        ///
        /// Korábban ez `Serializable` tranzakcióval és háromszoros újrapróbálással volt
        /// megoldva. Az helyes volt, de a legnehezebb eszköz egy számláló növeléséhez, és
        /// volt egy MÉRETLEN rése: a `40001` szerializációs hibát a szűrő csak nyers
        /// `PostgresException` alakban fogta, miközben az EF Core az `UPDATE`-nél keletkező
        /// hibát `DbUpdateException`-be csomagolja. Párhuzamos feladat-létrehozásnál ez
        /// kezeletlen kivétel, azaz 500 lett volna. Az atomi utasítás mellett ez a rés
        /// **nem létezik**: nincs szerializációs hiba, amit el lehetne hibázni, és nincs
        /// szükség újrapróbálásra sem.
        ///
        /// A `WITH … RETURNING` alak azért kell, mert a PostgreSQL az `UPDATE … RETURNING`-et
        /// önmagában nem engedi allekérdezésként - az EF `SqlQuery` pedig becsomagolhatja. A
        /// skalár `SqlQuery&lt;T&gt;` emellett `Value` nevű oszlopot vár.
        /// </remarks>
        public async Task<IReadOnlyList<long>> GetNextTaskNumbersAsync(Guid projectId, int count)
        {
            if (count < 1)
                throw new ValidationException("A kért sorszámok száma legalább 1 legyen!");

            var lastNums = await _context.Database
                .SqlQuery<long>($"""
                    WITH updated AS (
                        UPDATE "ProjectCounters"
                        SET "LastNum" = "LastNum" + {count}
                        WHERE "ProjectId" = {projectId}
                        RETURNING "LastNum"
                    )
                    SELECT "LastNum" AS "Value" FROM updated
                    """)
                .ToListAsync();

            if (lastNums.Count == 0)
                throw new NotFoundException("Számláló nem található");

            //A visszaadott érték a növelés UTÁNI utolsó sorszám, tehát a kiadott tartomány
            //ezzel végződik; a kezdete innen számolva adódik.
            var lastNum = lastNums[0];
            var startNum = lastNum - count + 1;

            return Enumerable.Range(0, count)
                             .Select(i => startNum + i)
                             .ToList();
        }
    }
}
