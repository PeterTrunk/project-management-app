using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectManager.API.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUniqueFromProjKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Projects_ProjKey",
                table: "Projects");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ProjKey",
                table: "Projects",
                column: "ProjKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Projects_ProjKey",
                table: "Projects");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ProjKey",
                table: "Projects",
                column: "ProjKey",
                unique: true);
        }
    }
}
