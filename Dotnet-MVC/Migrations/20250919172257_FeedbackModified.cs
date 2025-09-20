using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotnetMVCApp.Migrations
{
    /// <inheritdoc />
    public partial class FeedbackModified : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FeedbackText",
                table: "Feedbacks",
                newName: "FeedbackUrl");

            migrationBuilder.Sql(
       @"ALTER TABLE ""Feedbacks"" 
          ALTER COLUMN ""Sentiment"" TYPE integer 
          USING CASE 
              WHEN ""Sentiment"" ~ '^-?[0-9]+$' THEN ""Sentiment""::integer
              ELSE 0
          END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FeedbackUrl",
                table: "Feedbacks",
                newName: "FeedbackText");

            migrationBuilder.AlterColumn<string>(
                name: "Sentiment",
                table: "Feedbacks",
                type: "text",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
