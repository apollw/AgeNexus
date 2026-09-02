using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchPerformanceStatistics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "match_statistics_reports",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    match_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_by_player_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    submitted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confirmed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    awarded_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    replay_file_name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    replay_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    extractor_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    coverage_details = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_match_statistics_reports", x => x.id);
                    table.ForeignKey(
                        name: "fk_match_statistics_report_match",
                        column: x => x.match_id,
                        principalSchema: "public",
                        principalTable: "matches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_match_statistics_report_submitter",
                        column: x => x.submitted_by_player_profile_id,
                        principalSchema: "public",
                        principalTable: "player_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "player_match_statistics",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    match_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origin = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    units_killed = table.Column<int>(type: "integer", nullable: true),
                    units_lost = table.Column<int>(type: "integer", nullable: true),
                    buildings_destroyed = table.Column<int>(type: "integer", nullable: true),
                    buildings_lost = table.Column<int>(type: "integer", nullable: true),
                    largest_army = table.Column<int>(type: "integer", nullable: true),
                    peak_villagers = table.Column<int>(type: "integer", nullable: true),
                    food_collected = table.Column<long>(type: "bigint", nullable: true),
                    wood_collected = table.Column<long>(type: "bigint", nullable: true),
                    gold_collected = table.Column<long>(type: "bigint", nullable: true),
                    stone_collected = table.Column<long>(type: "bigint", nullable: true),
                    military_score = table.Column<int>(type: "integer", nullable: true),
                    economy_score = table.Column<int>(type: "integer", nullable: true),
                    technology_score = table.Column<int>(type: "integer", nullable: true),
                    society_score = table.Column<int>(type: "integer", nullable: true),
                    total_score = table.Column<int>(type: "integer", nullable: true),
                    units_converted = table.Column<int>(type: "integer", nullable: true),
                    trade_gold = table.Column<long>(type: "bigint", nullable: true),
                    relic_gold = table.Column<long>(type: "bigint", nullable: true),
                    tribute_sent = table.Column<long>(type: "bigint", nullable: true),
                    tribute_received = table.Column<long>(type: "bigint", nullable: true),
                    research_count = table.Column<int>(type: "integer", nullable: true),
                    explored_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    feudal_age_seconds = table.Column<int>(type: "integer", nullable: true),
                    castle_age_seconds = table.Column<int>(type: "integer", nullable: true),
                    imperial_age_seconds = table.Column<int>(type: "integer", nullable: true),
                    effective_actions_per_minute = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_match_statistics", x => x.id);
                    table.CheckConstraint("ck_player_match_statistics_explored", "explored_percent IS NULL OR (explored_percent >= 0 AND explored_percent <= 100)");
                    table.ForeignKey(
                        name: "fk_player_match_statistics_match",
                        column: x => x.match_id,
                        principalSchema: "public",
                        principalTable: "matches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_player_match_statistics_player",
                        column: x => x.player_profile_id,
                        principalSchema: "public",
                        principalTable: "player_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_player_match_statistics_report",
                        column: x => x.report_id,
                        principalSchema: "public",
                        principalTable: "match_statistics_reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_player_match_statistics_team",
                        column: x => x.team_id,
                        principalSchema: "public",
                        principalTable: "match_teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "player_performance_scores",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    match_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    military = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: false),
                    economy = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: false),
                    technology = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: false),
                    society = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: false),
                    overall = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: false),
                    award_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    bonus_points = table.Column<int>(type: "integer", nullable: false),
                    formula_version = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_performance_scores", x => x.id);
                    table.ForeignKey(
                        name: "fk_player_performance_score_match",
                        column: x => x.match_id,
                        principalSchema: "public",
                        principalTable: "matches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_player_performance_score_player",
                        column: x => x.player_profile_id,
                        principalSchema: "public",
                        principalTable: "player_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_player_performance_score_report",
                        column: x => x.report_id,
                        principalSchema: "public",
                        principalTable: "match_statistics_reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "statistics_confirmations",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    confirmed_by_player_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decision = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    decided_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_statistics_confirmations", x => x.id);
                    table.ForeignKey(
                        name: "fk_statistics_confirmation_player",
                        column: x => x.confirmed_by_player_profile_id,
                        principalSchema: "public",
                        principalTable: "player_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_statistics_confirmation_report",
                        column: x => x.report_id,
                        principalSchema: "public",
                        principalTable: "match_statistics_reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_statistics_confirmation_team",
                        column: x => x.team_id,
                        principalSchema: "public",
                        principalTable: "match_teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_match_statistics_reports_submitted_by_player_profile_id",
                schema: "public",
                table: "match_statistics_reports",
                column: "submitted_by_player_profile_id");

            migrationBuilder.CreateIndex(
                name: "ux_match_statistics_reports_match",
                schema: "public",
                table: "match_statistics_reports",
                column: "match_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_match_statistics_reports_replay_hash",
                schema: "public",
                table: "match_statistics_reports",
                column: "replay_sha256",
                unique: true,
                filter: "replay_sha256 IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_player_match_statistics_match_id",
                schema: "public",
                table: "player_match_statistics",
                column: "match_id");

            migrationBuilder.CreateIndex(
                name: "IX_player_match_statistics_player_profile_id",
                schema: "public",
                table: "player_match_statistics",
                column: "player_profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_player_match_statistics_team_id",
                schema: "public",
                table: "player_match_statistics",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "ux_player_match_statistics_report_player",
                schema: "public",
                table: "player_match_statistics",
                columns: new[] { "report_id", "player_profile_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_player_performance_scores_match_id",
                schema: "public",
                table: "player_performance_scores",
                column: "match_id");

            migrationBuilder.CreateIndex(
                name: "IX_player_performance_scores_player_profile_id",
                schema: "public",
                table: "player_performance_scores",
                column: "player_profile_id");

            migrationBuilder.CreateIndex(
                name: "ux_player_performance_scores_report_player",
                schema: "public",
                table: "player_performance_scores",
                columns: new[] { "report_id", "player_profile_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_statistics_confirmations_confirmed_by_player_profile_id",
                schema: "public",
                table: "statistics_confirmations",
                column: "confirmed_by_player_profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_statistics_confirmations_team_id",
                schema: "public",
                table: "statistics_confirmations",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "ux_statistics_confirmations_report_team",
                schema: "public",
                table: "statistics_confirmations",
                columns: new[] { "report_id", "team_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "player_match_statistics",
                schema: "public");

            migrationBuilder.DropTable(
                name: "player_performance_scores",
                schema: "public");

            migrationBuilder.DropTable(
                name: "statistics_confirmations",
                schema: "public");

            migrationBuilder.DropTable(
                name: "match_statistics_reports",
                schema: "public");
        }
    }
}
