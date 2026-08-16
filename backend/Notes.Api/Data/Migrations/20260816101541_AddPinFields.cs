using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notes.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPinFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPinned",
                table: "Notes",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PinnedAt",
                table: "Notes",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPinned",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "PinnedAt",
                table: "Notes");
        }
    }
}
