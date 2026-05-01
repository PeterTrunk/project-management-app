using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectManager.API.Migrations
{
    /// <inheritdoc />
    public partial class RenameStoragePathToStorageKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "StoragePath",
                table: "Attachments",
                newName: "StorageKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "StorageKey",
                table: "Attachments",
                newName: "StoragePath");
        }
    }
}
