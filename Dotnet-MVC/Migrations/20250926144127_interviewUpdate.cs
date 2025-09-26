using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotnetMVCApp.Migrations
{
    /// <inheritdoc />
    public partial class interviewUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AudioUrl",
                table: "Interviews");

            migrationBuilder.DropColumn(
                name: "VideoUrl",
                table: "Interviews");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AudioUrl",
                table: "Interviews",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VideoUrl",
                table: "Interviews",
                type: "text",
                nullable: true);
        }
    }
}
