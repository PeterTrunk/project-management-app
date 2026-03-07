using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectManager.API.Migrations
{
    /// <inheritdoc />
    public partial class AddUrlFieldsToCommitAndPrLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PrUrl",
                table: "PrLinks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommitUrl",
                table: "CommitLinks",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrUrl",
                table: "PrLinks");

            migrationBuilder.DropColumn(
                name: "CommitUrl",
                table: "CommitLinks");
        }
    }
}
