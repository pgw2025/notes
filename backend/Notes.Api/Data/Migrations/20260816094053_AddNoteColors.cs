using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notes.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNoteColors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BackgroundColor",
                table: "NoteVersions",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "BackgroundColor",
                table: "Notes",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "DefaultNoteColor",
                table: "AspNetUsers",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BackgroundColor",
                table: "NoteVersions");

            migrationBuilder.DropColumn(
                name: "BackgroundColor",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "DefaultNoteColor",
                table: "AspNetUsers");
        }
    }
}
