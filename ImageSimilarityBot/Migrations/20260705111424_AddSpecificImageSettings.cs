using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ImageSimilarityBot.Migrations
{
    /// <inheritdoc />
    public partial class AddSpecificImageSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Bannable",
                table: "SourceImages",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SimilarityThreshold",
                table: "SourceImages",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceImageId",
                table: "AttachmentHistories",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Stale",
                table: "AttachmentHistories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_AttachmentHistories_SourceImageId",
                table: "AttachmentHistories",
                column: "SourceImageId");

            migrationBuilder.AddForeignKey(
                name: "FK_AttachmentHistories_SourceImages_SourceImageId",
                table: "AttachmentHistories",
                column: "SourceImageId",
                principalTable: "SourceImages",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AttachmentHistories_SourceImages_SourceImageId",
                table: "AttachmentHistories");

            migrationBuilder.DropIndex(
                name: "IX_AttachmentHistories_SourceImageId",
                table: "AttachmentHistories");

            migrationBuilder.DropColumn(
                name: "Bannable",
                table: "SourceImages");

            migrationBuilder.DropColumn(
                name: "SimilarityThreshold",
                table: "SourceImages");

            migrationBuilder.DropColumn(
                name: "SourceImageId",
                table: "AttachmentHistories");

            migrationBuilder.DropColumn(
                name: "Stale",
                table: "AttachmentHistories");
        }
    }
}
