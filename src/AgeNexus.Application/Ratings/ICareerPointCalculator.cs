namespace AgeNexus.Application.Ratings;

public interface ICareerPointCalculator
{
    CareerPointCalculation Calculate(CareerPointCalculationRequest request);
}

public sealed record CareerPointCalculationRequest(Guid MatchId);
public sealed record CareerPointCalculation(IReadOnlyCollection<PlayerCareerPoints> Awards);
public sealed record PlayerCareerPoints(Guid PlayerId, int Points);
