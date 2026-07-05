using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace ImageSimilarityBot.Migrations
{
    /// <inheritdoc />
    public partial class VecChange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Vector>(
                name: "Embedding",
                table: "SourceImages",
                type: "vector(3072)",
                nullable: false,
                oldClrType: typeof(Vector),
                oldType: "vector(4096)");

            migrationBuilder.AlterColumn<Vector>(
                name: "Embedding",
                table: "AttachmentHistories",
                type: "vector(3072)",
                nullable: false,
                oldClrType: typeof(Vector),
                oldType: "vector(4096)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Vector>(
                name: "Embedding",
                table: "SourceImages",
                type: "vector(4096)",
                nullable: false,
                oldClrType: typeof(Vector),
                oldType: "vector(3072)");

            migrationBuilder.AlterColumn<Vector>(
                name: "Embedding",
                table: "AttachmentHistories",
                type: "vector(4096)",
                nullable: false,
                oldClrType: typeof(Vector),
                oldType: "vector(3072)");
        }
    }
}
