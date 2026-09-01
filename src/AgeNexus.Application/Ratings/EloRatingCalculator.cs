using AgeNexus.Domain.Competition;

namespace AgeNexus.Application.Ratings;

public sealed class EloRatingCalculator(ScoringRuleSet rules) : IRatingCalculator
{
    public RatingCalculation Calculate(RatingCalculationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Teams.Count != 2)
        {
            throw new ArgumentException("Elo calculation currently requires exactly two teams.", nameof(request));
        }

        var teams = request.Teams.ToArray();
        ValidateTeam(teams[0]);
        ValidateTeam(teams[1]);
        var strengths = teams.ToDictionary(
            x => x.TeamId,
            x => rules.CalculateEffectiveTeamStrength(x.Participants.Select(p => p.CurrentRating).ToArray()));
        var deltas = new List<PlayerRatingDelta>();

        for (var index = 0; index < teams.Length; index++)
        {
            var team = teams[index];
            var opponent = teams[1 - index];
            var expected = rules.CalculateExpectedScore(strengths[team.TeamId], strengths[opponent.TeamId]);
            var result = ToScoringResult(team.Result);

            deltas.AddRange(team.Participants
                .Where(x => !x.IsAi && x.PlayerId.HasValue)
                .Select(x => new PlayerRatingDelta(
                    x.PlayerId!.Value,
                    rules.CalculateRatingDelta(
                        expected,
                        result,
                        x.MatchesPlayed,
                        request.ModalityWeight,
                        request.CategoryFactor))));
        }

        return new RatingCalculation(deltas);
    }

    private static void ValidateTeam(RatedTeam team)
    {
        if (team.TeamId == Guid.Empty || team.Participants.Count == 0 || team.Result is < 0m or > 1m)
        {
            throw new ArgumentException("Rated teams require an id, participants and a result between zero and one.");
        }
    }

    private static ScoringResult ToScoringResult(decimal result) => result switch
    {
        1m => ScoringResult.Victory,
        0.5m => ScoringResult.Draw,
        0m => ScoringResult.Defeat,
        _ => throw new ArgumentOutOfRangeException(nameof(result), "Result must be 0, 0.5 or 1.")
    };
}
