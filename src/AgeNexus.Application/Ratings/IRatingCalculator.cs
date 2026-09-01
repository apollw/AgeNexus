namespace AgeNexus.Application.Ratings;

public interface IRatingCalculator
{
    RatingCalculation Calculate(RatingCalculationRequest request);
}

public sealed record RatingCalculationRequest(
    IReadOnlyCollection<RatedTeam> Teams,
    decimal ModalityWeight,
    decimal CategoryFactor = 1m);

public sealed record RatedTeam(
    Guid TeamId,
    decimal Result,
    IReadOnlyCollection<RatedParticipant> Participants);

public sealed record RatedParticipant(
    Guid? PlayerId,
    decimal CurrentRating,
    int MatchesPlayed,
    bool IsAi = false);

public sealed record RatingCalculation(IReadOnlyCollection<PlayerRatingDelta> Deltas);
public sealed record PlayerRatingDelta(Guid PlayerId, decimal Delta);
