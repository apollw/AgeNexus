using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteManualMatchStatistics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "castles_built",
                schema: "public",
                table: "player_match_statistics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "relics_captured",
                schema: "public",
                table: "player_match_statistics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "research_percent",
                schema: "public",
                table: "player_match_statistics",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "wonders_built",
                schema: "public",
                table: "player_match_statistics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_player_match_statistics_research",
                schema: "public",
                table: "player_match_statistics",
                sql: "research_percent IS NULL OR (research_percent >= 0 AND research_percent <= 100)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_player_match_statistics_research",
                schema: "public",
                table: "player_match_statistics");

            migrationBuilder.DropColumn(
                name: "castles_built",
                schema: "public",
                table: "player_match_statistics");

            migrationBuilder.DropColumn(
                name: "relics_captured",
                schema: "public",
                table: "player_match_statistics");

            migrationBuilder.DropColumn(
                name: "research_percent",
                schema: "public",
                table: "player_match_statistics");

            migrationBuilder.DropColumn(
                name: "wonders_built",
                schema: "public",
                table: "player_match_statistics");
        }
    }
}
