using AgeNexus.Domain.Common;

namespace AgeNexus.Domain.Matches;

public sealed class MatchRevision
{
    private MatchRevision()
    {
        Reason = null!;
        Snapshot = null!;
    }

    public MatchRevision(
        Guid id,
        Guid matchId,
        Guid changedByApplicationUserId,
        string reason,
        string snapshot,
        DateTimeOffset changedAtUtc)
    {
        if (id == Guid.Empty || matchId == Guid.Empty || changedByApplicationUserId == Guid.Empty ||
            string.IsNullOrWhiteSpace(reason) || string.IsNullOrWhiteSpace(snapshot) || changedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new DomainRuleException("Revision requires ids, reason, snapshot and UTC change time.");
        }

        Id = id;
        MatchId = matchId;
        ChangedByApplicationUserId = changedByApplicationUserId;
        Reason = reason.Trim();
        Snapshot = snapshot.Trim();
        ChangedAtUtc = changedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid MatchId { get; private set; }
    public Guid ChangedByApplicationUserId { get; private set; }
    public string Reason { get; private set; }
    public string Snapshot { get; private set; }
    public DateTimeOffset ChangedAtUtc { get; private set; }
}
