using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Common.Exceptions;

namespace ProjectManager.API.Common.Data
{
    /// <summary>
    /// A kliens által visszaküldött konkurencia-token ellenőrzése és beállítása.
    /// </summary>
    /// <remarks>
    /// A kódbázisban kilenc helyen szerepelt ez a két sor:
    ///
    /// <code>_context.Entry(entitas).OriginalValues["xmin"] = dto.RowVersion;</code>
    ///
    /// Ez önmagában elég ahhoz, hogy az EF a tokent a módosító utasítás <c>WHERE</c>
    /// záradékába tegye - de VAN egy rése, és a rés alakja mérésből derült ki.
    ///
    /// Az eredeti érték átírása magát az <c>xmin</c> tulajdonságot jelöli módosítottnak,
    /// tehát az entitás <c>Modified</c> lesz. Az <c>xmin</c> viszont tároló által generált,
    /// ezért nem kerül a <c>SET</c> záradékba - a kérdés az, hogy marad-e egyáltalán bármi,
    /// amit írni kell. Ha a kérés egyetlen tulajdonságot sem változtat meg (a módosító DTO
    /// minden mezője <c>null</c>), akkor ez azon fordul meg, hogy a
    /// <c>AppDbContext.SaveChangesAsync</c> talál-e <c>UpdatedAt</c> mezőt, amit
    /// bélyegezhet. A felülvizsgálat idején a négy védett entitás közül háromnak volt, az
    /// oszlopdefiníciónak nem - vagyis a védelem egy TIMESTAMP-MEZŐ MEGLÉTÉN állt, nem
    /// tervezésen. Ahol nem volt mit írni, ott <c>UPDATE</c> sem készült, és egy ELAVULT
    /// tokennel érkező kérés is <c>200</c>-at kapott. A kliens ebből azt olvassa, hogy a
    /// kezében lévő token érvényes volt, pedig nem - és a következő, immár tartalmas
    /// mentését arra épít.
    ///
    /// Az oszlopdefiníció azóta megkapta a hiányzó időbélyeget, tehát a mentéskori
    /// ellenőrzés ma mind a négy entitásnál egyformán működik. Ez a helper mégsem
    /// fölösleges: attól függetlenné teszi a védelmet, hogy egy entitásnak VAN-E
    /// időbélyege. Egy jövőbeli entitás ugyanabba a csapdába esne nélküle.
    ///
    /// Ez a helper ezért az ellenőrzést kimondottan elvégzi, a mentés előtt. Ahol a token
    /// egyezik, ott a beállítás amúgy is műveletmentes (a frissen betöltött eredeti érték
    /// már az), tehát a helper nem helyettesíti az EF mentéskori ellenőrzését, hanem
    /// <b>kiegészíti</b>: a mentéskori zárja a két lépés közötti rést, ez pedig megadja a
    /// hiányzó, determinisztikus kiindulópontot.
    ///
    /// A név szándékosan egyezik a kliens oldali <c>requireRowVersion</c> őrrel: ugyanaz az
    /// állítás a két végen - token nélkül nincs írás.
    /// </remarks>
    public static class ConcurrencyGuard
    {
        /// <summary>
        /// Ellenőrzi, hogy a kliens tokenje a sor JELENLEGI tokenje-e, majd beállítja
        /// eredeti értékként, hogy az EF a mentéskor is ellenőrizze.
        /// </summary>
        /// <param name="context">A követő környezet.</param>
        /// <param name="entity">A már betöltött, követett entitás.</param>
        /// <param name="clientRowVersion">A kliens által visszaküldött token.</param>
        /// <param name="conflictMessage">A felhasználónak szóló üzenet ütközés esetén.</param>
        /// <exception cref="ConflictException">
        /// Ha a sort a kliens betöltése óta módosították.
        /// </exception>
        public static void RequireRowVersion(
            this DbContext context, object entity, uint clientRowVersion, string conflictMessage)
        {
            var entry = context.Entry(entity);

            //A frissen betöltött érték, vagyis a sor jelenlegi tokenje. A nem generikus
            //EntityEntry csak object-et ad, ezért kell a cast; a tulajdonság mind a négy
            //konkurencia-tokennel védett entitáson uint.
            var current = (uint)entry.Property("xmin").CurrentValue!;

            if (current != clientRowVersion)
                throw new ConflictException(conflictMessage);

            entry.OriginalValues["xmin"] = clientRowVersion;
        }
    }
}
