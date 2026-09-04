using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgeNexus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgeNexusDbContext))]
[Migration("20260904120000_AddReadModelIndexes")]
public partial class AddReadModelIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "ix_matches_status",
            schema: "public",
            table: "matches",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "ix_match_statistics_reports_status_match",
            schema: "public",
            table: "match_statistics_reports",
            columns: new[] { "status", "match_id" });

        migrationBuilder.CreateIndex(
            name: "ix_rating_events_ranking",
            schema: "public",
            table: "rating_events",
            columns: new[] { "scope", "season_id", "beneficiary_id" });

        migrationBuilder.CreateIndex(
            name: "ix_point_events_ranking",
            schema: "public",
            table: "point_events",
            columns: new[] { "scope", "season_id", "beneficiary_id" });

        migrationBuilder.CreateIndex(
            name: "ix_point_events_verified_ranking",
            schema: "public",
            table: "point_events",
            columns: new[] { "scope", "evidence_level", "season_id", "beneficiary_id" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_matches_status",
            schema: "public",
            table: "matches");

        migrationBuilder.DropIndex(
            name: "ix_match_statistics_reports_status_match",
            schema: "public",
            table: "match_statistics_reports");

        migrationBuilder.DropIndex(
            name: "ix_rating_events_ranking",
            schema: "public",
            table: "rating_events");

        migrationBuilder.DropIndex(
            name: "ix_point_events_ranking",
            schema: "public",
            table: "point_events");

        migrationBuilder.DropIndex(
            name: "ix_point_events_verified_ranking",
            schema: "public",
            table: "point_events");
    }
}
