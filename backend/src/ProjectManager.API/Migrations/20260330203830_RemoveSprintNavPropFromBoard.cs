using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectManager.API.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSprintNavPropFromBoard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sprints_Boards_BoardId",
                table: "Sprints");

            migrationBuilder.DropIndex(
                name: "IX_Sprints_BoardId",
                table: "Sprints");

            migrationBuilder.DropColumn(
                name: "BoardId",
                table: "Sprints");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BoardId",
                table: "Sprints",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sprints_BoardId",
                table: "Sprints",
                column: "BoardId");

            migrationBuilder.AddForeignKey(
                name: "FK_Sprints_Boards_BoardId",
                table: "Sprints",
                column: "BoardId",
                principalTable: "Boards",
                principalColumn: "Id");
        }
    }
}
