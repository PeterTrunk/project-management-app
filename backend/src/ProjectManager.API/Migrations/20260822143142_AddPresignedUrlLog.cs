using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectManager.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPresignedUrlLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PresignedUrlLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    StorageKey = table.Column<string>(type: "text", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PresignedUrlLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PresignedUrlLogs_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PresignedUrlLogs_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PresignedUrlLogs_Confirmed",
                table: "PresignedUrlLogs",
                column: "Confirmed");

            migrationBuilder.CreateIndex(
                name: "IX_PresignedUrlLogs_CreatedById",
                table: "PresignedUrlLogs",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_PresignedUrlLogs_ExpiresAt",
                table: "PresignedUrlLogs",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_PresignedUrlLogs_ProjectId",
                table: "PresignedUrlLogs",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_PresignedUrlLogs_StorageKey",
                table: "PresignedUrlLogs",
                column: "StorageKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PresignedUrlLogs");
        }
    }
}
