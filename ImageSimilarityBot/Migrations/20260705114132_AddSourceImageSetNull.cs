using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ImageSimilarityBot.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceImageSetNull : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AttachmentHistories_SourceImages_SourceImageId",
                table: "AttachmentHistories");

            migrationBuilder.AddForeignKey(
                name: "FK_AttachmentHistories_SourceImages_SourceImageId",
                table: "AttachmentHistories",
                column: "SourceImageId",
                principalTable: "SourceImages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AttachmentHistories_SourceImages_SourceImageId",
                table: "AttachmentHistories");

            migrationBuilder.AddForeignKey(
                name: "FK_AttachmentHistories_SourceImages_SourceImageId",
                table: "AttachmentHistories",
                column: "SourceImageId",
                principalTable: "SourceImages",
                principalColumn: "Id");
        }
    }
}
