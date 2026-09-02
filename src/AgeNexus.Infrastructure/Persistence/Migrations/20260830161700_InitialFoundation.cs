using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "ai_difficulties",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_edition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    internal_level = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_difficulties", x => x.id);
                    table.CheckConstraint("ck_ai_difficulties_internal_level", "internal_level BETWEEN 1 AND 5");
                });

            migrationBuilder.CreateTable(
                name: "player_profiles",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    application_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_profiles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "matches",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_edition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_player_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    played_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    nature = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_matches", x => x.id);
                    table.ForeignKey(
                        name: "fk_matches_created_by_player_profile",
                        column: x => x.created_by_player_profile_id,
                        principalSchema: "public",
                        principalTable: "player_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "match_teams",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    result = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    match_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_match_teams", x => x.id);
                    table.ForeignKey(
                        name: "fk_match_teams_match",
                        column: x => x.match_id,
                        principalSchema: "public",
                        principalTable: "matches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "match_participants",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    player_profile_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ai_difficulty_id = table.Column<Guid>(type: "uuid", nullable: true),
                    faction_id = table.Column<Guid>(type: "uuid", nullable: true),
                    faction_selection = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_match_participants", x => x.id);
                    table.CheckConstraint("ck_match_participants_identity", "(type = 'Human' AND player_profile_id IS NOT NULL AND ai_difficulty_id IS NULL) OR (type = 'ArtificialIntelligence' AND player_profile_id IS NULL AND ai_difficulty_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_match_participants_ai_difficulty",
                        column: x => x.ai_difficulty_id,
                        principalSchema: "public",
                        principalTable: "ai_difficulties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_match_participants_player_profile",
                        column: x => x.player_profile_id,
                        principalSchema: "public",
                        principalTable: "player_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_match_participants_team",
                        column: x => x.team_id,
                        principalSchema: "public",
                        principalTable: "match_teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_ai_difficulties_edition_name",
                schema: "public",
                table: "ai_difficulties",
                columns: new[] { "game_edition_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_match_participants_ai_difficulty",
                schema: "public",
                table: "match_participants",
                column: "ai_difficulty_id");

            migrationBuilder.CreateIndex(
                name: "ix_match_participants_faction",
                schema: "public",
                table: "match_participants",
                column: "faction_id");

            migrationBuilder.CreateIndex(
                name: "ix_match_participants_player_profile",
                schema: "public",
                table: "match_participants",
                column: "player_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_match_participants_team",
                schema: "public",
                table: "match_participants",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "ux_match_teams_match_position",
                schema: "public",
                table: "match_teams",
                columns: new[] { "match_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_matches_created_by_player_profile",
                schema: "public",
                table: "matches",
                column: "created_by_player_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_matches_edition_status",
                schema: "public",
                table: "matches",
                columns: new[] { "game_edition_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_matches_played_at_utc",
                schema: "public",
                table: "matches",
                column: "played_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_player_profiles_display_name",
                schema: "public",
                table: "player_profiles",
                column: "display_name");

            migrationBuilder.CreateIndex(
                name: "ux_player_profiles_application_user_id",
                schema: "public",
                table: "player_profiles",
                column: "application_user_id",
                unique: true,
                filter: "application_user_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "match_participants",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ai_difficulties",
                schema: "public");

            migrationBuilder.DropTable(
                name: "match_teams",
                schema: "public");

            migrationBuilder.DropTable(
                name: "matches",
                schema: "public");

            migrationBuilder.DropTable(
                name: "player_profiles",
                schema: "public");
        }
    }
}

