using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotnetMVCApp.Migrations
{
    /// <inheritdoc />
    public partial class AddSaltToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ClerkId",
                table: "AppUsers",
                newName: "Salt");

            migrationBuilder.AddColumn<string>(
                name: "Password",
                table: "AppUsers",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Password",
                table: "AppUsers");

            migrationBuilder.RenameColumn(
                name: "Salt",
                table: "AppUsers",
                newName: "ClerkId");
        }
    }
}
