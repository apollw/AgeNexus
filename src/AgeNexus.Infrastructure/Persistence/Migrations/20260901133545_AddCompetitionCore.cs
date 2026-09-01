using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompetitionCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "game_patch_id",
                schema: "public",
                table: "matches",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "map_definition_id",
                schema: "public",
                table: "matches",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "season_id",
                schema: "public",
                table: "matches",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "clans",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    tag = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    created_by_player_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clans", x => x.id);
                    table.ForeignKey(
                        name: "fk_clans_founder",
                        column: x => x.created_by_player_profile_id,
                        principalSchema: "public",
                        principalTable: "player_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "games",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_games", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "match_confirmations",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    match_id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decision = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    decided_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_match_confirmations", x => x.id);
                    table.ForeignKey(
                        name: "fk_match_confirmations_match",
                        column: x => x.match_id,
                        principalSchema: "public",
                        principalTable: "matches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_match_confirmations_player",
                        column: x => x.player_profile_id,
                        principalSchema: "public",
                        principalTable: "player_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "match_evidence",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    match_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_by_player_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    submitted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    object_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    external_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_match_evidence", x => x.id);
                    table.ForeignKey(
                        name: "fk_match_evidence_match",
                        column: x => x.match_id,
                        principalSchema: "public",
                        principalTable: "matches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_match_evidence_submitter",
                        column: x => x.submitted_by_player_profile_id,
                        principalSchema: "public",
                        principalTable: "player_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "match_revisions",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    match_id = table.Column<Guid>(type: "uuid", nullable: false),
                    changed_by_application_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    changed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_match_revisions", x => x.id);
                    table.ForeignKey(
                        name: "fk_match_revisions_match",
                        column: x => x.match_id,
                        principalSchema: "public",
                        principalTable: "matches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "point_events",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    match_id = table.Column<Guid>(type: "uuid", nullable: false),
                    beneficiary_id = table.Column<Guid>(type: "uuid", nullable: false),
                    season_id = table.Column<Guid>(type: "uuid", nullable: true),
                    scope = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    points = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    rule_version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    calculation_details = table.Column<string>(type: "jsonb", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    source_key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    evidence_level = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    reverses_event_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_point_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rating_events",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    match_id = table.Column<Guid>(type: "uuid", nullable: false),
                    beneficiary_id = table.Column<Guid>(type: "uuid", nullable: false),
                    season_id = table.Column<Guid>(type: "uuid", nullable: true),
                    scope = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    delta = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    rule_version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    calculation_details = table.Column<string>(type: "jsonb", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    reverses_event_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rating_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "verification_decisions",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    match_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    decided_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    decided_by_application_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_verification_decisions", x => x.id);
                    table.ForeignKey(
                        name: "fk_verification_decisions_match",
                        column: x => x.match_id,
                        principalSchema: "public",
                        principalTable: "matches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "clan_memberships",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    clan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clan_memberships", x => x.id);
                    table.ForeignKey(
                        name: "fk_clan_memberships_clan",
                        column: x => x.clan_id,
                        principalSchema: "public",
                        principalTable: "clans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_clan_memberships_player",
                        column: x => x.player_profile_id,
                        principalSchema: "public",
                        principalTable: "player_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "game_editions",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_game_editions", x => x.id);
                    table.ForeignKey(
                        name: "fk_game_editions_game",
                        column: x => x.game_id,
                        principalSchema: "public",
                        principalTable: "games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "challenge_tickets",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_edition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    configuration_fingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    issued_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    used_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_challenge_tickets", x => x.id);
                    table.ForeignKey(
                        name: "fk_challenge_tickets_edition",
                        column: x => x.game_edition_id,
                        principalSchema: "public",
                        principalTable: "game_editions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_challenge_tickets_player",
                        column: x => x.player_profile_id,
                        principalSchema: "public",
                        principalTable: "player_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "factions",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_edition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    image_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_factions", x => x.id);
                    table.ForeignKey(
                        name: "fk_factions_game_edition",
                        column: x => x.game_edition_id,
                        principalSchema: "public",
                        principalTable: "game_editions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "game_patches",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_edition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    effective_from_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    effective_to_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_game_patches", x => x.id);
                    table.ForeignKey(
                        name: "fk_game_patches_game_edition",
                        column: x => x.game_edition_id,
                        principalSchema: "public",
                        principalTable: "game_editions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "map_definitions",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_edition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_map_definitions", x => x.id);
                    table.ForeignKey(
                        name: "fk_maps_game_edition",
                        column: x => x.game_edition_id,
                        principalSchema: "public",
                        principalTable: "game_editions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "seasons",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_edition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    starts_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_seasons", x => x.id);
                    table.CheckConstraint("ck_seasons_interval", "ends_at_utc > starts_at_utc");
                    table.ForeignKey(
                        name: "fk_seasons_game_edition",
                        column: x => x.game_edition_id,
                        principalSchema: "public",
                        principalTable: "game_editions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "team_lineups",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_edition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    normalized_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_team_lineups", x => x.id);
                    table.ForeignKey(
                        name: "fk_team_lineups_game_edition",
                        column: x => x.game_edition_id,
                        principalSchema: "public",
                        principalTable: "game_editions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "player_favorite_factions",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    faction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_favorite_factions", x => x.id);
                    table.CheckConstraint("ck_player_favorite_factions_priority", "priority BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "fk_player_favorite_factions_faction",
                        column: x => x.faction_id,
                        principalSchema: "public",
                        principalTable: "factions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_player_favorite_factions_player",
                        column: x => x.player_profile_id,
                        principalSchema: "public",
                        principalTable: "player_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "team_lineup_members",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    team_lineup_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_team_lineup_members", x => x.id);
                    table.ForeignKey(
                        name: "fk_team_lineup_members_lineup",
                        column: x => x.team_lineup_id,
                        principalSchema: "public",
                        principalTable: "team_lineups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_team_lineup_members_player",
                        column: x => x.player_profile_id,
                        principalSchema: "public",
                        principalTable: "player_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_matches_map",
                schema: "public",
                table: "matches",
                column: "map_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_matches_patch",
                schema: "public",
                table: "matches",
                column: "game_patch_id");

            migrationBuilder.CreateIndex(
                name: "ix_matches_season",
                schema: "public",
                table: "matches",
                column: "season_id");

            migrationBuilder.CreateIndex(
                name: "IX_challenge_tickets_game_edition_id",
                schema: "public",
                table: "challenge_tickets",
                column: "game_edition_id");

            migrationBuilder.CreateIndex(
                name: "IX_challenge_tickets_player_profile_id",
                schema: "public",
                table: "challenge_tickets",
                column: "player_profile_id");

            migrationBuilder.CreateIndex(
                name: "ux_challenge_tickets_code",
                schema: "public",
                table: "challenge_tickets",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_clan_memberships_clan_id",
                schema: "public",
                table: "clan_memberships",
                column: "clan_id");

            migrationBuilder.CreateIndex(
                name: "ux_clan_memberships_active_player",
                schema: "public",
                table: "clan_memberships",
                column: "player_profile_id",
                unique: true,
                filter: "ended_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_clans_created_by_player_profile_id",
                schema: "public",
                table: "clans",
                column: "created_by_player_profile_id");

            migrationBuilder.CreateIndex(
                name: "ux_clans_name",
                schema: "public",
                table: "clans",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_clans_tag",
                schema: "public",
                table: "clans",
                column: "tag",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_factions_edition_slug",
                schema: "public",
                table: "factions",
                columns: new[] { "game_edition_id", "slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_game_editions_game_slug",
                schema: "public",
                table: "game_editions",
                columns: new[] { "game_id", "slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_game_patches_edition_start",
                schema: "public",
                table: "game_patches",
                columns: new[] { "game_edition_id", "effective_from_utc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_games_slug",
                schema: "public",
                table: "games",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_maps_edition_slug",
                schema: "public",
                table: "map_definitions",
                columns: new[] { "game_edition_id", "slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_match_confirmations_player_profile_id",
                schema: "public",
                table: "match_confirmations",
                column: "player_profile_id");

            migrationBuilder.CreateIndex(
                name: "ux_match_confirmations_match_player",
                schema: "public",
                table: "match_confirmations",
                columns: new[] { "match_id", "player_profile_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_match_evidence_match_id",
                schema: "public",
                table: "match_evidence",
                column: "match_id");

            migrationBuilder.CreateIndex(
                name: "IX_match_evidence_submitted_by_player_profile_id",
                schema: "public",
                table: "match_evidence",
                column: "submitted_by_player_profile_id");

            migrationBuilder.CreateIndex(
                name: "ux_match_evidence_sha256",
                schema: "public",
                table: "match_evidence",
                column: "sha256",
                unique: true,
                filter: "sha256 IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_match_revisions_match_time",
                schema: "public",
                table: "match_revisions",
                columns: new[] { "match_id", "changed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_player_favorite_factions_faction_id",
                schema: "public",
                table: "player_favorite_factions",
                column: "faction_id");

            migrationBuilder.CreateIndex(
                name: "ux_player_favorite_factions_player_faction",
                schema: "public",
                table: "player_favorite_factions",
                columns: new[] { "player_profile_id", "faction_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_player_favorite_factions_player_priority",
                schema: "public",
                table: "player_favorite_factions",
                columns: new[] { "player_profile_id", "priority" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_point_events_repetition",
                schema: "public",
                table: "point_events",
                columns: new[] { "beneficiary_id", "season_id", "source_key" });

            migrationBuilder.CreateIndex(
                name: "ux_point_events_idempotency",
                schema: "public",
                table: "point_events",
                columns: new[] { "match_id", "beneficiary_id", "season_id", "scope", "rule_version", "kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_point_events_reversal",
                schema: "public",
                table: "point_events",
                column: "reverses_event_id",
                unique: true,
                filter: "reverses_event_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_rating_events_idempotency",
                schema: "public",
                table: "rating_events",
                columns: new[] { "match_id", "beneficiary_id", "season_id", "scope", "rule_version", "kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_rating_events_reversal",
                schema: "public",
                table: "rating_events",
                column: "reverses_event_id",
                unique: true,
                filter: "reverses_event_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_seasons_edition_start",
                schema: "public",
                table: "seasons",
                columns: new[] { "game_edition_id", "starts_at_utc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_team_lineup_members_player_profile_id",
                schema: "public",
                table: "team_lineup_members",
                column: "player_profile_id");

            migrationBuilder.CreateIndex(
                name: "ux_team_lineup_members_player",
                schema: "public",
                table: "team_lineup_members",
                columns: new[] { "team_lineup_id", "player_profile_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_team_lineup_members_position",
                schema: "public",
                table: "team_lineup_members",
                columns: new[] { "team_lineup_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_team_lineups_game_edition_id",
                schema: "public",
                table: "team_lineups",
                column: "game_edition_id");

            migrationBuilder.CreateIndex(
                name: "ux_team_lineups_key",
                schema: "public",
                table: "team_lineups",
                column: "normalized_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_verification_decisions_match_time",
                schema: "public",
                table: "verification_decisions",
                columns: new[] { "match_id", "decided_at_utc" });

            migrationBuilder.AddForeignKey(
                name: "fk_ai_difficulties_game_edition",
                schema: "public",
                table: "ai_difficulties",
                column: "game_edition_id",
                principalSchema: "public",
                principalTable: "game_editions",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_match_participants_faction",
                schema: "public",
                table: "match_participants",
                column: "faction_id",
                principalSchema: "public",
                principalTable: "factions",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_matches_game_edition",
                schema: "public",
                table: "matches",
                column: "game_edition_id",
                principalSchema: "public",
                principalTable: "game_editions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_matches_map",
                schema: "public",
                table: "matches",
                column: "map_definition_id",
                principalSchema: "public",
                principalTable: "map_definitions",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_matches_patch",
                schema: "public",
                table: "matches",
                column: "game_patch_id",
                principalSchema: "public",
                principalTable: "game_patches",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_matches_season",
                schema: "public",
                table: "matches",
                column: "season_id",
                principalSchema: "public",
                principalTable: "seasons",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_ai_difficulties_game_edition",
                schema: "public",
                table: "ai_difficulties");

            migrationBuilder.DropForeignKey(
                name: "fk_match_participants_faction",
                schema: "public",
                table: "match_participants");

            migrationBuilder.DropForeignKey(
                name: "fk_matches_game_edition",
                schema: "public",
                table: "matches");

            migrationBuilder.DropForeignKey(
                name: "fk_matches_map",
                schema: "public",
                table: "matches");

            migrationBuilder.DropForeignKey(
                name: "fk_matches_patch",
                schema: "public",
                table: "matches");

            migrationBuilder.DropForeignKey(
                name: "fk_matches_season",
                schema: "public",
                table: "matches");

            migrationBuilder.DropTable(
                name: "challenge_tickets",
                schema: "public");

            migrationBuilder.DropTable(
                name: "clan_memberships",
                schema: "public");

            migrationBuilder.DropTable(
                name: "game_patches",
                schema: "public");

            migrationBuilder.DropTable(
                name: "map_definitions",
                schema: "public");

            migrationBuilder.DropTable(
                name: "match_confirmations",
                schema: "public");

            migrationBuilder.DropTable(
                name: "match_evidence",
                schema: "public");

            migrationBuilder.DropTable(
                name: "match_revisions",
                schema: "public");

            migrationBuilder.DropTable(
                name: "player_favorite_factions",
                schema: "public");

            migrationBuilder.DropTable(
                name: "point_events",
                schema: "public");

            migrationBuilder.DropTable(
                name: "rating_events",
                schema: "public");

            migrationBuilder.DropTable(
                name: "seasons",
                schema: "public");

            migrationBuilder.DropTable(
                name: "team_lineup_members",
                schema: "public");

            migrationBuilder.DropTable(
                name: "verification_decisions",
                schema: "public");

            migrationBuilder.DropTable(
                name: "clans",
                schema: "public");

            migrationBuilder.DropTable(
                name: "factions",
                schema: "public");

            migrationBuilder.DropTable(
                name: "team_lineups",
                schema: "public");

            migrationBuilder.DropTable(
                name: "game_editions",
                schema: "public");

            migrationBuilder.DropTable(
                name: "games",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "ix_matches_map",
                schema: "public",
                table: "matches");

            migrationBuilder.DropIndex(
                name: "ix_matches_patch",
                schema: "public",
                table: "matches");

            migrationBuilder.DropIndex(
                name: "ix_matches_season",
                schema: "public",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "game_patch_id",
                schema: "public",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "map_definition_id",
                schema: "public",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "season_id",
                schema: "public",
                table: "matches");
        }
    }
}
