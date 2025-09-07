using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace STWiki.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Activities_Users_UserEntityId",
                table: "Activities");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Users_UserId",
                table: "Users",
                column: "UserId");

            migrationBuilder.CreateTable(
                name: "MediaFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    StoredFileName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BucketName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    AltText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UploadedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: true),
                    Height = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaFiles_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MediaThumbnails",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MediaFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoredFileName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BucketName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaThumbnails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaThumbnails_MediaFiles_MediaFileId",
                        column: x => x.MediaFileId,
                        principalTable: "MediaFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PageMediaReferences",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PageId = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageMediaReferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PageMediaReferences_MediaFiles_MediaFileId",
                        column: x => x.MediaFileId,
                        principalTable: "MediaFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PageMediaReferences_Pages_PageId",
                        column: x => x.PageId,
                        principalTable: "Pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_IsDeleted_IsPublic",
                table: "MediaFiles",
                columns: new[] { "IsDeleted", "IsPublic" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_OriginalFileName",
                table: "MediaFiles",
                column: "OriginalFileName");

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_UploadedAt",
                table: "MediaFiles",
                column: "UploadedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_UploadedByUserId",
                table: "MediaFiles",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaThumbnails_MediaFileId_Width",
                table: "MediaThumbnails",
                columns: new[] { "MediaFileId", "Width" });

            migrationBuilder.CreateIndex(
                name: "IX_PageMediaReferences_MediaFileId",
                table: "PageMediaReferences",
                column: "MediaFileId");

            migrationBuilder.CreateIndex(
                name: "IX_PageMediaReferences_PageId",
                table: "PageMediaReferences",
                column: "PageId");

            migrationBuilder.AddForeignKey(
                name: "FK_Activities_Users_UserId",
                table: "Activities",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Activities_Users_UserId",
                table: "Activities");

            migrationBuilder.DropTable(
                name: "MediaThumbnails");

            migrationBuilder.DropTable(
                name: "PageMediaReferences");

            migrationBuilder.DropTable(
                name: "MediaFiles");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Users_UserId",
                table: "Users");

            migrationBuilder.AddForeignKey(
                name: "FK_Activities_Users_UserEntityId",
                table: "Activities",
                column: "UserEntityId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
