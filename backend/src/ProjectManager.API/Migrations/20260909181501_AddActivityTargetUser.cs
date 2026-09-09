using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectManager.API.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityTargetUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TargetUserId",
                table: "Activities",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Activities_TargetUserId",
                table: "Activities",
                column: "TargetUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Activities_Users_TargetUserId",
                table: "Activities",
                column: "TargetUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Activities_Users_TargetUserId",
                table: "Activities");

            migrationBuilder.DropIndex(
                name: "IX_Activities_TargetUserId",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "TargetUserId",
                table: "Activities");
        }
    }
}
