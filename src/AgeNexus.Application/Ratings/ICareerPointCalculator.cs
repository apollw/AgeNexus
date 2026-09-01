using AgeNexus.Domain.Competition;
using AgeNexus.Domain.Matches;

namespace AgeNexus.Application.Ratings;

public interface ICareerPointCalculator
{
    CareerPointCalculation Calculate(CareerPointCalculationRequest request);
}

public sealed record CareerPointCalculationRequest(
    MatchScoringCategory Category,
    int LargestHumanTeamSize,
    IReadOnlyCollection<CareerTeam> Teams);

public sealed record CareerTeam(
    IReadOnlyCollection<Guid> PlayerIds,
    int HumanTeamSize,
    int OpponentHumanTeamSize,
    ScoringResult Result,
    decimal ExpectedScore);

public sealed record CareerPointCalculation(IReadOnlyCollection<PlayerCareerPoints> Awards);
public sealed record PlayerCareerPoints(Guid PlayerId, int Points);
