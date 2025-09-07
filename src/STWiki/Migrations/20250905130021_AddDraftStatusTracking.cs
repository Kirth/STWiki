using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STWiki.Migrations
{
    /// <inheritdoc />
    public partial class AddDraftStatusTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasUncommittedChanges",
                table: "Pages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastCommittedAt",
                table: "Pages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastCommittedContent",
                table: "Pages",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastDraftAt",
                table: "Pages",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasUncommittedChanges",
                table: "Pages");

            migrationBuilder.DropColumn(
                name: "LastCommittedAt",
                table: "Pages");

            migrationBuilder.DropColumn(
                name: "LastCommittedContent",
                table: "Pages");

            migrationBuilder.DropColumn(
                name: "LastDraftAt",
                table: "Pages");
        }
    }
}
