using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectManager.API.Migrations
{
    /// <inheritdoc />
    public partial class UpdateGitIntegrationModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PrLinks_RepoFullName_PrNumber",
                table: "PrLinks");

            migrationBuilder.DropIndex(
                name: "IX_PrLinks_TaskId_Provider_RepoFullName_PrNumber",
                table: "PrLinks");

            migrationBuilder.DropIndex(
                name: "IX_Integrations_ProjectId_Provider",
                table: "Integrations");

            migrationBuilder.DropIndex(
                name: "IX_CommitLinks_RepoFullName_CommitSha",
                table: "CommitLinks");

            migrationBuilder.DropIndex(
                name: "IX_CommitLinks_TaskId_CommitSha",
                table: "CommitLinks");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "PrLinks");

            migrationBuilder.DropColumn(
                name: "RepoFullName",
                table: "PrLinks");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "CommitLinks");

            migrationBuilder.DropColumn(
                name: "RepoFullName",
                table: "CommitLinks");

            migrationBuilder.AddColumn<string>(
                name: "AuthorName",
                table: "PrLinks",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "IntegrationId",
                table: "PrLinks",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<string>(
                name: "AccessToken",
                table: "Integrations",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "WebhookToken",
                table: "Integrations",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AuthorName",
                table: "CommitLinks",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "IntegrationId",
                table: "CommitLinks",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_PrLinks_IntegrationId_PrNumber",
                table: "PrLinks",
                columns: new[] { "IntegrationId", "PrNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrLinks_TaskId",
                table: "PrLinks",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_Integrations_ProjectId_Provider_RepoFullName",
                table: "Integrations",
                columns: new[] { "ProjectId", "Provider", "RepoFullName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Integrations_WebhookToken",
                table: "Integrations",
                column: "WebhookToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommitLinks_IntegrationId_CommitSha",
                table: "CommitLinks",
                columns: new[] { "IntegrationId", "CommitSha" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommitLinks_TaskId",
                table: "CommitLinks",
                column: "TaskId");

            migrationBuilder.AddForeignKey(
                name: "FK_CommitLinks_Integrations_IntegrationId",
                table: "CommitLinks",
                column: "IntegrationId",
                principalTable: "Integrations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PrLinks_Integrations_IntegrationId",
                table: "PrLinks",
                column: "IntegrationId",
                principalTable: "Integrations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CommitLinks_Integrations_IntegrationId",
                table: "CommitLinks");

            migrationBuilder.DropForeignKey(
                name: "FK_PrLinks_Integrations_IntegrationId",
                table: "PrLinks");

            migrationBuilder.DropIndex(
                name: "IX_PrLinks_IntegrationId_PrNumber",
                table: "PrLinks");

            migrationBuilder.DropIndex(
                name: "IX_PrLinks_TaskId",
                table: "PrLinks");

            migrationBuilder.DropIndex(
                name: "IX_Integrations_ProjectId_Provider_RepoFullName",
                table: "Integrations");

            migrationBuilder.DropIndex(
                name: "IX_Integrations_WebhookToken",
                table: "Integrations");

            migrationBuilder.DropIndex(
                name: "IX_CommitLinks_IntegrationId_CommitSha",
                table: "CommitLinks");

            migrationBuilder.DropIndex(
                name: "IX_CommitLinks_TaskId",
                table: "CommitLinks");

            migrationBuilder.DropColumn(
                name: "AuthorName",
                table: "PrLinks");

            migrationBuilder.DropColumn(
                name: "IntegrationId",
                table: "PrLinks");

            migrationBuilder.DropColumn(
                name: "WebhookToken",
                table: "Integrations");

            migrationBuilder.DropColumn(
                name: "AuthorName",
                table: "CommitLinks");

            migrationBuilder.DropColumn(
                name: "IntegrationId",
                table: "CommitLinks");

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "PrLinks",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RepoFullName",
                table: "PrLinks",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "AccessToken",
                table: "Integrations",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "CommitLinks",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RepoFullName",
                table: "CommitLinks",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_PrLinks_RepoFullName_PrNumber",
                table: "PrLinks",
                columns: new[] { "RepoFullName", "PrNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_PrLinks_TaskId_Provider_RepoFullName_PrNumber",
                table: "PrLinks",
                columns: new[] { "TaskId", "Provider", "RepoFullName", "PrNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Integrations_ProjectId_Provider",
                table: "Integrations",
                columns: new[] { "ProjectId", "Provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommitLinks_RepoFullName_CommitSha",
                table: "CommitLinks",
                columns: new[] { "RepoFullName", "CommitSha" });

            migrationBuilder.CreateIndex(
                name: "IX_CommitLinks_TaskId_CommitSha",
                table: "CommitLinks",
                columns: new[] { "TaskId", "CommitSha" },
                unique: true);
        }
    }
}
