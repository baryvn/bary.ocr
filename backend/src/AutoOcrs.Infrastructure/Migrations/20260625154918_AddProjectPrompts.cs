using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoOcrs.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectPrompts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClassificationPrompt",
                table: "projects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractionPrompt",
                table: "projects",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClassificationPrompt",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "ExtractionPrompt",
                table: "projects");
        }
    }
}
