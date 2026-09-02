using AgeNexus.Domain.MatchPerformance;
using AgeNexus.Domain.Matches;

namespace AgeNexus.Application.MatchPerformance;

public interface IPerformanceCalculator
{
    PerformanceCalculation Calculate(PerformanceCalculationRequest request);
}

public sealed record PerformancePlayerInput(
    Guid PlayerProfileId,
    Guid TeamId,
    TeamResult TeamResult,
    int MilitaryScore,
    int EconomyScore,
    int TechnologyScore,
    int SocietyScore);

public sealed record PerformanceCalculationRequest(
    MatchScoringCategory Category,
    int LargestHumanTeamSize,
    IReadOnlyCollection<PerformancePlayerInput> Players);

public sealed record PlayerPerformanceResult(
    Guid PlayerProfileId,
    Guid TeamId,
    decimal Military,
    decimal Economy,
    decimal Technology,
    decimal Society,
    decimal Overall,
    PerformanceAwardType AwardType,
    int BonusPoints);

public sealed record PerformanceCalculation(
    string FormulaVersion,
    IReadOnlyCollection<PlayerPerformanceResult> Players);
