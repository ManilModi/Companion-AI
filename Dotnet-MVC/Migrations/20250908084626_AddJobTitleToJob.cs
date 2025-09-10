using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotnetMVCApp.Migrations
{
    /// <inheritdoc />
    public partial class AddJobTitleToJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "JobTitle",
                table: "Jobs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JobTitle",
                table: "Jobs");
        }
    }
}
