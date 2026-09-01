using AgeNexus.Domain.Competition;
using AgeNexus.Domain.EvidenceAndModeration;

namespace AgeNexus.Application.Ratings;

public interface IPvePointCalculator
{
    PvePointCalculation Calculate(PvePointCalculationRequest request);
}

public sealed record PvePointCalculationRequest(
    IReadOnlyCollection<Guid> HumanPlayerIds,
    IReadOnlyCollection<int> AiInternalLevels,
    ScoringResult Result,
    int PreviousEquivalentWins,
    EvidenceLevel EvidenceLevel,
    decimal BasicEvidencePointsAlreadyAwarded);

public sealed record PvePointAward(Guid PlayerId, decimal PvePoints, decimal CareerPoints);
public sealed record PvePointCalculation(IReadOnlyCollection<PvePointAward> Awards);
