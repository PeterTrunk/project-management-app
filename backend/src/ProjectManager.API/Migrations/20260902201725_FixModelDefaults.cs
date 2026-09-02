using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectManager.API.Migrations
{
    /// <inheritdoc />
    public partial class FixModelDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Priority",
                table: "ProjectTasks",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true,
                defaultValue: "none",
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16,
                oldNullable: true,
                oldDefaultValue: "normal");

            migrationBuilder.AlterColumn<string>(
                name: "Position",
                table: "ProjectTasks",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "0");

            migrationBuilder.AlterColumn<int>(
                name: "EstimateInMinutes",
                table: "ProjectTasks",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true,
                oldDefaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Priority",
                table: "ProjectTasks",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true,
                defaultValue: "normal",
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16,
                oldNullable: true,
                oldDefaultValue: "none");

            migrationBuilder.AlterColumn<string>(
                name: "Position",
                table: "ProjectTasks",
                type: "text",
                nullable: false,
                defaultValue: "0",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "EstimateInMinutes",
                table: "ProjectTasks",
                type: "integer",
                nullable: true,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
