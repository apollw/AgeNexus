using AgeNexus.Domain.Competition;
using AgeNexus.Domain.Matches;

namespace AgeNexus.Application.Ratings;

public sealed class CareerPointCalculator(ScoringRuleSet rules) : ICareerPointCalculator
{
    public CareerPointCalculation Calculate(CareerPointCalculationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Category == MatchScoringCategory.PurePve)
        {
            throw new ArgumentException("Pure PvE career contribution is produced by the PvE calculator.", nameof(request));
        }

        var awards = new List<PlayerCareerPoints>();
        foreach (var team in request.Teams)
        {
            if (team.PlayerIds.Count == 0 || team.PlayerIds.Any(x => x == Guid.Empty))
            {
                throw new ArgumentException("Career teams require valid human players.", nameof(request));
            }

            var basePoints = rules.GetPvpCareerBase(request.LargestHumanTeamSize, team.Result);
            var asymmetricFactor = rules.GetAsymmetricCareerMultiplier(
                team.HumanTeamSize,
                team.OpponentHumanTeamSize,
                team.Result);
            var adjustedBase = decimal.ToInt32(Math.Round(
                basePoints * asymmetricFactor,
                0,
                MidpointRounding.AwayFromZero));
            var finalPoints = request.Category == MatchScoringCategory.HybridPvp
                ? rules.CalculateHybridCareerPoints(adjustedBase, team.ExpectedScore)
                : adjustedBase;

            awards.AddRange(team.PlayerIds.Select(x => new PlayerCareerPoints(x, finalPoints)));
        }

        return new CareerPointCalculation(awards);
    }
}
