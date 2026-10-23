using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectManager.API.Migrations
{
    /// <inheritdoc />
    public partial class FixActiveSprintIndexFilter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            //A szuro korabban 'active'-ot tartalmazott kisbetuvel, az alkalmazas viszont
            //'Active'-ot ir: az index igy egyetlen sorra sem illeszkedett, tehat a
            //"projektenkent egy aktiv sprint" garancia csendben hatastalan volt.
            //
            //Mivel a kenyszer eddig nem ervenyesult, LETEZHET mar olyan projekt, amelyben
            //egynel tobb 'Active' sprint van. Az egyedi index letrehozasa ilyenkor elbukna,
            //es a migracio egy nehezen erthetto hibaval allna meg. Ezert elotte kimondjuk,
            //mit kell tenni - a javitas szandekosan NEM automatikus: azt, hogy a tobbletbol
            //melyik sprint maradjon aktiv, csak a projekt tagjai tudjak eldonteni.
            migrationBuilder.Sql(@"
                DO $$
                DECLARE offending int;
                BEGIN
                    SELECT count(*) INTO offending FROM (
                        SELECT ""ProjectId"" FROM ""Sprints""
                        WHERE ""State"" = 'Active'
                        GROUP BY ""ProjectId"" HAVING count(*) > 1
                    ) t;

                    IF offending > 0 THEN
                        RAISE EXCEPTION
                            'A migracio nem futtathato: % projektben egynel tobb aktiv sprint van. '
                            'Listazas: SELECT ""ProjectId"", count(*) FROM ""Sprints"" '
                            'WHERE ""State"" = ''Active'' GROUP BY ""ProjectId"" HAVING count(*) > 1; '
                            'Projektenkent egy sprint maradjon ''Active'', a tobbi allitsd '
                            '''Completed'' vagy ''Planning'' allapotba, majd futtasd ujra.',
                            offending;
                    END IF;
                END $$;
            ");

            migrationBuilder.DropIndex(
                name: "IX_Sprints_OneActiveSprint",
                table: "Sprints");

            migrationBuilder.CreateIndex(
                name: "IX_Sprints_OneActiveSprint",
                table: "Sprints",
                column: "ProjectId",
                unique: true,
                filter: "\"State\" = 'Active'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sprints_OneActiveSprint",
                table: "Sprints");

            migrationBuilder.CreateIndex(
                name: "IX_Sprints_OneActiveSprint",
                table: "Sprints",
                column: "ProjectId",
                unique: true,
                filter: "\"State\" = 'active'");
        }
    }
}
