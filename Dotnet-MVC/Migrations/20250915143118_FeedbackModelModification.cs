using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotnetMVCApp.Migrations
{
    /// <inheritdoc />
    public partial class FeedbackModelModification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FeedbackText",
                table: "Feedbacks",
                newName: "FeedbackUrl");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FeedbackUrl",
                table: "Feedbacks",
                newName: "FeedbackText");
        }
    }
}
