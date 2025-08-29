using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STWiki.Migrations
{
    /// <inheritdoc />
    public partial class AddRevisionPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Revisions_PageId",
                table: "Revisions");

            migrationBuilder.CreateIndex(
                name: "IX_Revisions_CreatedAt",
                table: "Revisions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Revisions_PageId_CreatedAt",
                table: "Revisions",
                columns: new[] { "PageId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Revisions_CreatedAt",
                table: "Revisions");

            migrationBuilder.DropIndex(
                name: "IX_Revisions_PageId_CreatedAt",
                table: "Revisions");

            migrationBuilder.CreateIndex(
                name: "IX_Revisions_PageId",
                table: "Revisions",
                column: "PageId");
        }
    }
}
