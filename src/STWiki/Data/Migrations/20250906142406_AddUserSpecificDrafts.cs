using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STWiki.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSpecificDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Drafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PageId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    BaseContent = table.Column<string>(type: "text", nullable: true),
                    BaseRevisionId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Drafts_Pages_PageId",
                        column: x => x.PageId,
                        principalTable: "Pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Drafts_Revisions_BaseRevisionId",
                        column: x => x.BaseRevisionId,
                        principalTable: "Revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Drafts_BaseRevisionId",
                table: "Drafts",
                column: "BaseRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_Drafts_PageId",
                table: "Drafts",
                column: "PageId");

            migrationBuilder.CreateIndex(
                name: "IX_Drafts_UpdatedAt",
                table: "Drafts",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Drafts_UserId",
                table: "Drafts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Drafts_UserId_PageId",
                table: "Drafts",
                columns: new[] { "UserId", "PageId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Drafts");
        }
    }
}
