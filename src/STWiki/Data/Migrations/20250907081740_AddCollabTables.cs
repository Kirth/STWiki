using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace STWiki.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCollabTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CollabSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PageId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CheckpointVersion = table.Column<long>(type: "bigint", nullable: false),
                    CheckpointBytes = table.Column<byte[]>(type: "bytea", nullable: true),
                    AwarenessJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollabSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollabSessions_Pages_PageId",
                        column: x => x.PageId,
                        principalTable: "Pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CollabCheckpoints",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    SnapshotBytes = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollabCheckpoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollabCheckpoints_CollabSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "CollabSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CollabUpdates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    VectorClockJson = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    UpdateBytes = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollabUpdates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollabUpdates_CollabSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "CollabSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CollabCheckpoints_SessionId_Version",
                table: "CollabCheckpoints",
                columns: new[] { "SessionId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollabSessions_PageId",
                table: "CollabSessions",
                column: "PageId");

            migrationBuilder.CreateIndex(
                name: "IX_CollabSessions_PageId_ClosedAt",
                table: "CollabSessions",
                columns: new[] { "PageId", "ClosedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CollabUpdates_CreatedAt",
                table: "CollabUpdates",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CollabUpdates_SessionId_Id",
                table: "CollabUpdates",
                columns: new[] { "SessionId", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CollabCheckpoints");

            migrationBuilder.DropTable(
                name: "CollabUpdates");

            migrationBuilder.DropTable(
                name: "CollabSessions");
        }
    }
}
