using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectManager.API.Migrations
{
    /// <summary>
    /// UpdatedAt időbélyeg az oszlopdefiníciókra.
    /// </summary>
    /// <remarks>
    /// Három lépés, nem egy. Az EF alapból egyetlen `AddColumn`-t generált
    /// `defaultValue: 0001-01-01`-lel, és az a DEFAULT megmaradt volna az oszlopon - a modell
    /// viszont nem ismer alapértéket (a másik három entitás UpdatedAt-je sem), tehát a séma
    /// és a modell elcsúszott volna egymástól. Ezért: felvétel nullable-ként, feltöltés,
    /// majd NOT NULL - így nem marad hátra adatbázis-szintű alapérték.
    ///
    /// A MEGLÉVŐ sorok a migráció idejét kapják. Ez nem valódi módosítási idő, és nem is
    /// állítja magáról, hogy az: ezektől a soroktól egyszerűen nincs ilyen adat, a mező
    /// ezzel a migrációval kezd létezni. A mezőt egyébként sem olvassa ma semmi - a
    /// válaszobjektum nem adja ki -, tehát a feltöltött érték nem jelenik meg sehol.
    ///
    /// Az írás triggerszerű: az AppDbContext.SaveChangesAsync állítja be minden mentésnél,
    /// a szolgáltatások nem nyúlnak hozzá. Ezért nem törési változás.
    /// </remarks>
    public partial class AddUpdatedAtToColumnDefinition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "ColumnDefinitions"
                    ADD COLUMN "UpdatedAt" timestamp with time zone NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE "ColumnDefinitions" SET "UpdatedAt" = now() WHERE "UpdatedAt" IS NULL;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "ColumnDefinitions"
                    ALTER COLUMN "UpdatedAt" SET NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ColumnDefinitions");
        }
    }
}
