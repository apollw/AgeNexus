namespace AgeNexus.Application.Ratings;

public interface IRatingCalculator
{
    RatingCalculation Calculate(RatingCalculationRequest request);
}

public sealed record RatingCalculationRequest(
    IReadOnlyCollection<RatedTeam> Teams,
    decimal ModalityWeight);

public sealed record RatedTeam(
    Guid TeamId,
    decimal Result,
    IReadOnlyCollection<RatedPlayer> Players);

public sealed record RatedPlayer(Guid PlayerId, decimal CurrentRating, int MatchesPlayed);
public sealed record RatingCalculation(IReadOnlyCollection<PlayerRatingDelta> Deltas);
public sealed record PlayerRatingDelta(Guid PlayerId, decimal Delta);
