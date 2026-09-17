using AgeNexus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgeNexus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgeNexusDbContext))]
[Migration("20260917150000_AddAiParticipantStatistics")]
public sealed class AddAiParticipantStatistics : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "ai_difficulty_id",
            schema: "public",
            table: "player_match_statistics",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "match_participant_id",
            schema: "public",
            table: "player_match_statistics",
            type: "uuid",
            nullable: true);

        migrationBuilder.AlterColumn<Guid>(
            name: "player_profile_id",
            schema: "public",
            table: "player_match_statistics",
            type: "uuid",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uuid");

        migrationBuilder.Sql("""
            UPDATE public.player_match_statistics AS statistic
            SET match_participant_id = participant.id
            FROM public.match_participants AS participant
            INNER JOIN public.match_teams AS team ON team.id = participant.team_id
            WHERE statistic.match_id = team.match_id
              AND statistic.player_profile_id = participant.player_profile_id;

            ALTER TABLE public.player_match_statistics
            ALTER COLUMN match_participant_id SET NOT NULL;
            """);

        migrationBuilder.DropIndex(
            name: "ux_player_match_statistics_report_player",
            schema: "public",
            table: "player_match_statistics");

        migrationBuilder.AddCheckConstraint(
            name: "ck_player_match_statistics_identity",
            schema: "public",
            table: "player_match_statistics",
            sql: "(player_profile_id IS NOT NULL AND ai_difficulty_id IS NULL) OR (player_profile_id IS NULL AND ai_difficulty_id IS NOT NULL)");

        migrationBuilder.CreateIndex(
            name: "ix_player_match_statistics_ai_difficulty",
            schema: "public",
            table: "player_match_statistics",
            column: "ai_difficulty_id");

        migrationBuilder.CreateIndex(
            name: "ix_player_match_statistics_match_participant",
            schema: "public",
            table: "player_match_statistics",
            column: "match_participant_id");

        migrationBuilder.CreateIndex(
            name: "ux_player_match_statistics_report_participant",
            schema: "public",
            table: "player_match_statistics",
            columns: new[] { "report_id", "match_participant_id" },
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "fk_player_match_statistics_ai_difficulty",
            schema: "public",
            table: "player_match_statistics",
            column: "ai_difficulty_id",
            principalSchema: "public",
            principalTable: "ai_difficulties",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "fk_player_match_statistics_participant",
            schema: "public",
            table: "player_match_statistics",
            column: "match_participant_id",
            principalSchema: "public",
            principalTable: "match_participants",
            principalColumn: "id",
            onDelete: ReferentialAction.Cascade);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DELETE FROM public.player_match_statistics WHERE player_profile_id IS NULL;");

        migrationBuilder.DropForeignKey(
            name: "fk_player_match_statistics_ai_difficulty",
            schema: "public",
            table: "player_match_statistics");

        migrationBuilder.DropForeignKey(
            name: "fk_player_match_statistics_participant",
            schema: "public",
            table: "player_match_statistics");

        migrationBuilder.DropCheckConstraint(
            name: "ck_player_match_statistics_identity",
            schema: "public",
            table: "player_match_statistics");

        migrationBuilder.DropIndex(
            name: "ix_player_match_statistics_ai_difficulty",
            schema: "public",
            table: "player_match_statistics");

        migrationBuilder.DropIndex(
            name: "ix_player_match_statistics_match_participant",
            schema: "public",
            table: "player_match_statistics");

        migrationBuilder.DropIndex(
            name: "ux_player_match_statistics_report_participant",
            schema: "public",
            table: "player_match_statistics");

        migrationBuilder.DropColumn(
            name: "ai_difficulty_id",
            schema: "public",
            table: "player_match_statistics");

        migrationBuilder.DropColumn(
            name: "match_participant_id",
            schema: "public",
            table: "player_match_statistics");

        migrationBuilder.AlterColumn<Guid>(
            name: "player_profile_id",
            schema: "public",
            table: "player_match_statistics",
            type: "uuid",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "ux_player_match_statistics_report_player",
            schema: "public",
            table: "player_match_statistics",
            columns: new[] { "report_id", "player_profile_id" },
            unique: true);
    }
}
