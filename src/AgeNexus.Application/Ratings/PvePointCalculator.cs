using AgeNexus.Domain.Competition;
using AgeNexus.Domain.EvidenceAndModeration;

namespace AgeNexus.Application.Ratings;

public sealed class PvePointCalculator(ScoringRuleSet rules) : IPvePointCalculator
{
    public PvePointCalculation Calculate(PvePointCalculationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.HumanPlayerIds.Count == 0 || request.HumanPlayerIds.Any(x => x == Guid.Empty))
        {
            throw new ArgumentException("PvE calculation requires valid human players.", nameof(request));
        }

        var rawPoints = rules.CalculatePvePointsPerHuman(
            request.AiInternalLevels,
            request.HumanPlayerIds.Count,
            request.Result,
            request.PreviousEquivalentWins);
        var factor = EvidencePolicy.GetPvePointFactor(request.EvidenceLevel);
        var points = rawPoints * factor;

        if (request.EvidenceLevel == EvidenceLevel.Basic)
        {
            var remainingCap = Math.Max(0m, ScoringRuleSet.BasicEvidenceSeasonCap - request.BasicEvidencePointsAlreadyAwarded);
            points = Math.Min(points, remainingCap);
        }

        points = Math.Round(points, 2, MidpointRounding.AwayFromZero);
        var careerPoints = Math.Round(
            points * ScoringRuleSet.PveCareerContribution,
            2,
            MidpointRounding.AwayFromZero);
        return new PvePointCalculation(request.HumanPlayerIds
            .Select(x => new PvePointAward(x, points, careerPoints))
            .ToArray());
    }
}
