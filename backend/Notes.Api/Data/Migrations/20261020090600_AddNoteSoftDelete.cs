using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notes.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNoteSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Notes",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeletedFromCategoryId",
                table: "Notes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Notes",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "DeletedFromCategoryId",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Notes");
        }
    }
}
