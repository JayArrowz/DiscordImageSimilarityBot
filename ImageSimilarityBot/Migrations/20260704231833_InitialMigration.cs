using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Pgvector;

#nullable disable

namespace ImageSimilarityBot.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "AttachmentHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OriginalUrl = table.Column<string>(type: "text", nullable: false),
                    ProxyUrl = table.Column<string>(type: "text", nullable: false),
                    Hash = table.Column<string>(type: "text", nullable: false),
                    Embedding = table.Column<Vector>(type: "vector(4096)", nullable: false),
                    Blocked = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttachmentHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SourceImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Path = table.Column<string>(type: "text", nullable: false),
                    Embedding = table.Column<Vector>(type: "vector(4096)", nullable: false),
                    Hash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceImages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttachmentHistory_Hash",
                table: "AttachmentHistories",
                column: "Hash");

            migrationBuilder.CreateIndex(
                name: "IX_AttachmentHistory_Url",
                table: "AttachmentHistories",
                column: "OriginalUrl");

            migrationBuilder.CreateIndex(
                name: "IX_SourceImage_Hash",
                table: "SourceImages",
                column: "Hash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttachmentHistories");

            migrationBuilder.DropTable(
                name: "SourceImages");
        }
    }
}
