using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectManager.API.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftDeleteToColumnAndRefactorTaskStatusHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "TaskStatusHistories");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "ColumnDefinitions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ColumnDefinitions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ColumnDefinitions");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ColumnDefinitions");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "TaskStatusHistories",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");
        }
    }
}
