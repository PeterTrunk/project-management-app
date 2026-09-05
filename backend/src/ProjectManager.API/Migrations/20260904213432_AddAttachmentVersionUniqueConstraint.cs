using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectManager.API.Migrations
{
    /// <inheritdoc />
    public partial class AddAttachmentVersionUniqueConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Attachments_ProjectId",
                table: "Attachments");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_ProjectId_FileName_Version",
                table: "Attachments",
                columns: new[] { "ProjectId", "FileName", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Attachments_ProjectId_FileName_Version",
                table: "Attachments");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_ProjectId",
                table: "Attachments",
                column: "ProjectId");
        }
    }
}
