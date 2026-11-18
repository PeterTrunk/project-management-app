using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectManager.API.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationAccessTokenAndAutoBackfill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccessToken",
                table: "Integrations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutoBackfillEnabled",
                table: "Integrations",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccessToken",
                table: "Integrations");

            migrationBuilder.DropColumn(
                name: "AutoBackfillEnabled",
                table: "Integrations");
        }
    }
}
