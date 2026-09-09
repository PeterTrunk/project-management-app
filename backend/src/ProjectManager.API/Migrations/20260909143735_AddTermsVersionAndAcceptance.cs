using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectManager.API.Migrations
{
    /// <inheritdoc />
    public partial class AddTermsVersionAndAcceptance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TermsVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TermsVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserTermsAcceptances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TermsVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTermsAcceptances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserTermsAcceptances_TermsVersions_TermsVersionId",
                        column: x => x.TermsVersionId,
                        principalTable: "TermsVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserTermsAcceptances_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TermsVersions_EffectiveFrom",
                table: "TermsVersions",
                column: "EffectiveFrom");

            migrationBuilder.CreateIndex(
                name: "IX_TermsVersions_Version",
                table: "TermsVersions",
                column: "Version",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserTermsAcceptances_TermsVersionId",
                table: "UserTermsAcceptances",
                column: "TermsVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTermsAcceptances_UserId_TermsVersionId",
                table: "UserTermsAcceptances",
                columns: new[] { "UserId", "TermsVersionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserTermsAcceptances");

            migrationBuilder.DropTable(
                name: "TermsVersions");
        }
    }
}
