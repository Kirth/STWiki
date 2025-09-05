using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STWiki.Migrations
{
    /// <inheritdoc />
    public partial class AddDraftContentField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DraftContent",
                table: "Pages",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DraftContent",
                table: "Pages");
        }
    }
}
