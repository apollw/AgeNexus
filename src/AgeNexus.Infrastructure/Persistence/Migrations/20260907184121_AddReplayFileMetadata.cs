using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReplayFileMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "content_type",
                schema: "public",
                table: "match_evidence",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "file_name",
                schema: "public",
                table: "match_evidence",
                type: "character varying(260)",
                maxLength: 260,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "content_type",
                schema: "public",
                table: "match_evidence");

            migrationBuilder.DropColumn(
                name: "file_name",
                schema: "public",
                table: "match_evidence");
        }
    }
}
