using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectManager.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCommitBackfill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommitBackfills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IntegrationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ref = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    BeforeSha = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    AfterSha = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ReportedCommitCount = table.Column<int>(type: "integer", nullable: true),
                    ReceivedCommitCount = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "Pending"),
                    ClaimedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClaimedById = table.Column<Guid>(type: "uuid", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommitBackfills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommitBackfills_Integrations_IntegrationId",
                        column: x => x.IntegrationId,
                        principalTable: "Integrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CommitBackfills_Users_ClaimedById",
                        column: x => x.ClaimedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommitBackfills_ClaimedById",
                table: "CommitBackfills",
                column: "ClaimedById");

            migrationBuilder.CreateIndex(
                name: "IX_CommitBackfills_IntegrationId_AfterSha",
                table: "CommitBackfills",
                columns: new[] { "IntegrationId", "AfterSha" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommitBackfills_IntegrationId_State",
                table: "CommitBackfills",
                columns: new[] { "IntegrationId", "State" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommitBackfills");
        }
    }
}
