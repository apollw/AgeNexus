using AgeNexus.Domain.Common;

namespace AgeNexus.Domain.Matches;

public sealed class MatchConfirmation
{
    private MatchConfirmation()
    {
        Comment = null!;
    }

    public MatchConfirmation(
        Guid id,
        Guid matchId,
        Guid playerProfileId,
        ConfirmationDecision decision,
        DateTimeOffset decidedAtUtc,
        string? comment = null)
    {
        if (id == Guid.Empty || matchId == Guid.Empty || playerProfileId == Guid.Empty || decidedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new DomainRuleException("Confirmation requires ids and a UTC decision time.");
        }

        Id = id;
        MatchId = matchId;
        PlayerProfileId = playerProfileId;
        Decision = decision;
        DecidedAtUtc = decidedAtUtc;
        Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
    }

    public Guid Id { get; private set; }
    public Guid MatchId { get; private set; }
    public Guid PlayerProfileId { get; private set; }
    public ConfirmationDecision Decision { get; private set; }
    public DateTimeOffset DecidedAtUtc { get; private set; }
    public string? Comment { get; private set; }
}
