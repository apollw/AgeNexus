using AgeNexus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgeNexus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgeNexusDbContext))]
[Migration("20261030150000_CorrectSoloMvpAwards")]
public sealed class CorrectSoloMvpAwards : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            WITH solo_bonus AS (
                SELECT DISTINCT event.*
                FROM public.point_events AS event
                INNER JOIN public.match_teams AS team ON team.match_id = event.match_id
                INNER JOIN public.match_participants AS participant
                    ON participant.team_id = team.id
                   AND participant.player_profile_id = event.beneficiary_id
                WHERE event.scope = 'PerformanceBonus'
                  AND event.kind = 'Award'
                  AND (SELECT count(*) FROM public.match_participants AS member
                       WHERE member.team_id = team.id) = 1
            )
            INSERT INTO public.point_events
                (id, match_id, beneficiary_id, season_id, scope, points, rule_version,
                 calculation_details, created_at_utc, source_key, evidence_level, kind, reverses_event_id)
            SELECT md5(event.id::text || ':solo-mvp-reversal')::uuid,
                   event.match_id,
                   event.beneficiary_id,
                   event.season_id,
                   event.scope,
                   -event.points,
                   '2026.10-solo-mvp-reversal.1',
                   jsonb_build_object('reason', 'MVP removido de equipe individual', 'originalEventId', event.id),
                   now(),
                   event.source_key,
                   event.evidence_level,
                   'Reversal',
                   event.id
            FROM solo_bonus AS event
            WHERE NOT EXISTS (
                SELECT 1 FROM public.point_events AS reversal
                WHERE reversal.reverses_event_id = event.id
            );

            UPDATE public.player_performance_scores AS score
            SET award_type = 'None', bonus_points = 0
            FROM public.match_teams AS team
            INNER JOIN public.match_participants AS participant ON participant.team_id = team.id
            WHERE score.team_id = team.id
              AND participant.player_profile_id = score.player_profile_id
              AND (SELECT count(*) FROM public.match_participants AS member
                   WHERE member.team_id = team.id) = 1
              AND score.award_type IN ('Mvp', 'SharedMvp');

            UPDATE public.player_match_statistics AS statistic
            SET is_team_mvp = FALSE
            WHERE statistic.is_team_mvp
              AND (SELECT count(*) FROM public.match_participants AS member
                   WHERE member.team_id = statistic.team_id) = 1;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM public.point_events
            WHERE rule_version = '2026.10-solo-mvp-reversal.1'
              AND kind = 'Reversal';
            """);
    }
}
