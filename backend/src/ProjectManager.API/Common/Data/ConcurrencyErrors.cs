using Npgsql;

namespace ProjectManager.API.Common.Data
{
    /// <summary>
    /// A PostgreSQL konkurencia-hibakódjainak felismerése a kivétel-láncban.
    /// </summary>
    /// <remarks>
    /// Azért kell külön helper, mert a hibakód NEM mindig a legkülső kivételen látszik: az
    /// EF Core a PostgreSQL hibáját <c>DbUpdateException</c>-be csomagolja, ha az az
    /// <c>UPDATE</c> végrehajtásánál keletkezik, és csak a <c>COMMIT</c>-nál jövő hiba
    /// érkezik nyers <see cref="PostgresException"/> alakban. Egy csak a nyers alakra szűrő
    /// ág a csomagolt esetet átengedné - és mivel a middleware minden nem AppException-t
    /// 500-ra képez, a felhasználó félrevezető hibakódot kapna egy kezelt helyzetre.
    ///
    /// Ezt a láncbejárást a <c>SprintService</c> aktiválási ága vezette be; mivel a
    /// <c>Serializable</c> tranzakció hat helyen szerepel, a logika közös helyre került.
    /// </remarks>
    public static class ConcurrencyErrors
    {
        /// <summary>Szerializációs ütközés: a Serializable tranzakciót a szerver megszakította.</summary>
        public const string SerializationFailure = "40001";

        /// <summary>Egyediségi kényszer sértése - például a szűrt "egy aktív sprint" index.</summary>
        public const string UniqueViolation = "23505";

        /// <summary>
        /// Igaz, ha a kivétel-láncban szerializációs ütközés vagy egyediség-sértés található.
        /// Mindkettő ugyanazt jelenti a hívó szempontjából: valaki más ugyanezen az adaton
        /// dolgozott, a műveletet újra kell próbálni.
        /// </summary>
        public static bool IsConflict(Exception exception)
        {
            for (var current = exception; current != null; current = current.InnerException)
            {
                if (current is PostgresException postgres &&
                    (postgres.SqlState == SerializationFailure || postgres.SqlState == UniqueViolation))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
