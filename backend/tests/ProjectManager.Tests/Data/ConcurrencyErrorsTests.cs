using Microsoft.EntityFrameworkCore;
using Npgsql;
using ProjectManager.API.Common.Data;

namespace ProjectManager.Tests.Data
{
    /// <summary>
    /// A konkurencia-hibakódok felismerésének tesztjei.
    /// </summary>
    /// <remarks>
    /// Ennek a helpernek eddig NEM volt tesztje, pedig hat <c>Serializable</c> tranzakció és
    /// a csatolmány-feltöltés újrapróbálkozása is rajta áll. Amit elront, az nem látszik:
    /// egy fel nem ismert hibakód a generikus ágra fut, és a middleware <c>500</c>-at képez
    /// belőle egy olyan helyzetre, amire van kezelés.
    ///
    /// A tesztek két dolgot rögzítenek. Egyrészt a felismert kódok halmazát - a holtpont
    /// (<c>40P01</c>) utólag került be, és enélkül egy többsoros írás ütközése kezelhetetlen
    /// maradt volna. Másrészt a CSOMAGOLT alakot: az EF Core a PostgreSQL hibáját
    /// <c>DbUpdateException</c>-be teszi, ha az a módosító utasításnál keletkezik, és csak a
    /// <c>COMMIT</c>-nál jövő hiba érkezik nyersen. Ez volt az eredeti hibaosztály a
    /// számláló régi, sorosított változatában.
    /// </remarks>
    public class ConcurrencyErrorsTests
    {
        /// <summary>
        /// A valódi alakot utánozza: a Npgsql ezt a négy mezőt adja meg a kivételnek.
        /// </summary>
        private static PostgresException Postgres(string sqlState)
            => new("teszt hibaüzenet", "ERROR", "ERROR", sqlState);

        [Theory]
        [InlineData(ConcurrencyErrors.SerializationFailure)]   // 40001
        [InlineData(ConcurrencyErrors.DeadlockDetected)]       // 40P01
        [InlineData(ConcurrencyErrors.UniqueViolation)]        // 23505
        public void IsConflict_AKezeltHibakodokra_Igaz(string sqlState)
        {
            Assert.True(ConcurrencyErrors.IsConflict(Postgres(sqlState)));
        }

        [Fact]
        public void IsConflict_AHoltpontotIsFelismeri()
        {
            // Külön is kiírva, mert ez a kód utólag került be. A Serializable tranzakció
            // ütközéskor 40001-et AD, de ha két tranzakció eltérő sorrendben éri el
            // ugyanazokat a sorokat, a szerver 40P01-et ad helyette. A hívó szempontjából a
            // kettő ugyanaz: a művelet nem hajtódott végre, újra lehet próbálni.
            Assert.Equal("40P01", ConcurrencyErrors.DeadlockDetected);
            Assert.True(ConcurrencyErrors.IsConflict(Postgres("40P01")));
        }

        [Fact]
        public void IsConflict_ACsomagoltAlakotIsFelismeri()
        {
            // EZ A LÉNYEGI ÁLLÍTÁS: a hibakód nem a legkülső kivételen van.
            var wrapped = new DbUpdateException("Hiba a mentés során", Postgres("40001"));

            Assert.True(ConcurrencyErrors.IsConflict(wrapped));
        }

        [Fact]
        public void IsConflict_AMelyebbenCsomagoltAlakotIsFelismeri()
        {
            var deep = new InvalidOperationException("külső",
                new DbUpdateException("középső", Postgres("23505")));

            Assert.True(ConcurrencyErrors.IsConflict(deep));
        }

        [Theory]
        [InlineData("23503")]   // idegen kulcs sértés: NEM újrapróbálható
        [InlineData("23502")]   // not null sértés
        [InlineData("42P01")]   // nem létező tábla
        [InlineData("57014")]   // megszakított utasítás
        public void IsConflict_MasPostgresHibakra_Hamis(string sqlState)
        {
            // Ez a fele is állítás: egy idegen kulcs sértést vagy egy séma-hibát NEM szabad
            // "próbáld újra" helyzetként kezelni, mert az újrapróbálkozás ugyanúgy elbukik.
            Assert.False(ConcurrencyErrors.IsConflict(Postgres(sqlState)));
        }

        [Fact]
        public void IsConflict_NemAdatbazisHibara_Hamis()
        {
            Assert.False(ConcurrencyErrors.IsConflict(new InvalidOperationException("akármi")));
            Assert.False(ConcurrencyErrors.IsConflict(new DbUpdateConcurrencyException("token")));
        }

        [Fact]
        public void IsConflict_NemAHIBAUZENETRE_Szur()
        {
            // A csatolmány-feltöltésnél korábban `Message.Contains("unique")` alakú szűrő
            // állt. Az ilyen szűrő a szerver nyelvi beállításától függ, és MÁS kényszerre is
            // illeszkedik. Itt rögzítjük, hogy a helper a KÓDOT nézi: egy "unique" szót
            // tartalmazó, de nem egyediségi hiba nem minősül ütközésnek.
            var misleading = new DbUpdateException(
                "duplicate key value violates unique constraint", Postgres("23503"));

            Assert.False(ConcurrencyErrors.IsConflict(misleading));
        }
    }
}
