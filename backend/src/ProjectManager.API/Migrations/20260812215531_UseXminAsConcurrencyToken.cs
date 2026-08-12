using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectManager.API.Migrations
{
    /// <inheritdoc />
    public partial class UseXminAsConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Sprints");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ProjectTasks");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ColumnDefinitions");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Boards");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Sprints");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "ProjectTasks");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "ColumnDefinitions");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Boards");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Sprints",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ProjectTasks",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ColumnDefinitions",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Boards",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);
        }
    }
}
