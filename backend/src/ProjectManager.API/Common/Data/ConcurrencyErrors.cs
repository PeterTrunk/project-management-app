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
    ///
    /// <b>Miért SqlState és nem a hibaüzenet.</b> A hibakód a PostgreSQL szerződése, az
    /// üzenet nem: szövege a szerver verziójától és nyelvi beállításától függ. Egy
    /// <c>Message.Contains("unique")</c> alakú szűrő ezért csendben elromolhat egy
    /// verzióváltáskor, és - ami rosszabb - MÁS egyediségi kényszerre is illeszkedik, mint
    /// amire szánták. A csatolmány-feltöltésnél pontosan ez a szűrő állt, és emiatt egy
    /// elsődleges kulcs ütközése is "próbáld újra" ágra futott volna.
    /// </remarks>
    public static class ConcurrencyErrors
    {
        /// <summary>Szerializációs ütközés: a Serializable tranzakciót a szerver megszakította.</summary>
        public const string SerializationFailure = "40001";

        /// <summary>Egyediségi kényszer sértése - például a szűrt "egy aktív sprint" index.</summary>
        public const string UniqueViolation = "23505";

        /// <summary>
        /// Holtpont: a szerver két tranzakció közül az egyiket megszakította.
        /// </summary>
        /// <remarks>
        /// Azért tartozik ide, mert a hívó szempontjából ugyanazt jelenti, mint a
        /// szerializációs hiba: a műveletet nem hajtotta végre senki, újra lehet próbálni.
        ///
        /// Hogy a rendszerben ELŐFORDUL-e, azt nem mértük. Két többsoros írás van, amelyik
        /// elvben kiváltja (az oszlop-átrendezés és az újrasorszámozás): ha két tranzakció
        /// eltérő sorrendben éri el ugyanazokat a sorokat, a szerver holtpontot jelez a
        /// szerializációs hiba HELYETT. A felvétele azért indokolt mégis, mert a hiányából
        /// következő kár aszimmetrikus: ha nem fordul elő, ez a konstans nem csinál semmit;
        /// ha előfordul, nélküle egy kezelt helyzetből <c>500</c> lesz.
        /// </remarks>
        public const string DeadlockDetected = "40P01";

        /// <summary>
        /// Igaz, ha a exception chain-ben: szerializációs ütközés, holtpont vagy egyediség-sértés található.
        /// </summary>
        public static bool IsConflict(Exception exception)
        {
            for (var current = exception; current != null; current = current.InnerException)
            {
                if (current is PostgresException postgres && IsConflictState(postgres.SqlState))
                    return true;
            }

            return false;
        }

        private static bool IsConflictState(string sqlState)
            => sqlState == SerializationFailure
            || sqlState == DeadlockDetected
            || sqlState == UniqueViolation;
    }
}
