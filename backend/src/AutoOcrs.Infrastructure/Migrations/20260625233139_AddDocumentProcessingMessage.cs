using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoOcrs.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentProcessingMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProcessingMessage",
                table: "documents",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProcessingMessage",
                table: "documents");
        }
    }
}
