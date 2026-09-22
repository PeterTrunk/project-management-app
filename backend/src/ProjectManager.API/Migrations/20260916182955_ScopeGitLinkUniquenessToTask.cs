using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectManager.API.Migrations
{
    /// <inheritdoc />
    public partial class ScopeGitLinkUniquenessToTask : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PrLinks_IntegrationId_PrNumber",
                table: "PrLinks");

            migrationBuilder.DropIndex(
                name: "IX_CommitLinks_IntegrationId_CommitSha",
                table: "CommitLinks");

            migrationBuilder.CreateIndex(
                name: "IX_PrLinks_IntegrationId_PrNumber_TaskId",
                table: "PrLinks",
                columns: new[] { "IntegrationId", "PrNumber", "TaskId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_CommitLinks_IntegrationId_CommitSha_TaskId",
                table: "CommitLinks",
                columns: new[] { "IntegrationId", "CommitSha", "TaskId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PrLinks_IntegrationId_PrNumber_TaskId",
                table: "PrLinks");

            migrationBuilder.DropIndex(
                name: "IX_CommitLinks_IntegrationId_CommitSha_TaskId",
                table: "CommitLinks");

            migrationBuilder.CreateIndex(
                name: "IX_PrLinks_IntegrationId_PrNumber",
                table: "PrLinks",
                columns: new[] { "IntegrationId", "PrNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommitLinks_IntegrationId_CommitSha",
                table: "CommitLinks",
                columns: new[] { "IntegrationId", "CommitSha" },
                unique: true);
        }
    }
}
