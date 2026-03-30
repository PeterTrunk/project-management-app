using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectManager.API.Migrations
{
    /// <inheritdoc />
    public partial class RemoveBoardIdFromSprint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sprints_Boards_BoardId",
                table: "Sprints");

            migrationBuilder.AddForeignKey(
                name: "FK_Sprints_Boards_BoardId",
                table: "Sprints",
                column: "BoardId",
                principalTable: "Boards",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sprints_Boards_BoardId",
                table: "Sprints");

            migrationBuilder.AddForeignKey(
                name: "FK_Sprints_Boards_BoardId",
                table: "Sprints",
                column: "BoardId",
                principalTable: "Boards",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
