using AgeNexus.Domain.Common;

namespace AgeNexus.Domain.EvidenceAndModeration;

public sealed class VerificationDecision
{
    private VerificationDecision()
    {
        Reason = null!;
    }

    public VerificationDecision(
        Guid id,
        Guid matchId,
        VerificationStatus status,
        string reason,
        DateTimeOffset decidedAtUtc,
        Guid? decidedByApplicationUserId = null)
    {
        if (id == Guid.Empty || matchId == Guid.Empty || string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainRuleException("Decision, match and reason are required.");
        }

        if (decidedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new DomainRuleException("Decision time must be provided in UTC.");
        }

        Id = id;
        MatchId = matchId;
        Status = status;
        Reason = reason.Trim();
        DecidedAtUtc = decidedAtUtc;
        DecidedByApplicationUserId = decidedByApplicationUserId;
    }

    public Guid Id { get; private set; }
    public Guid MatchId { get; private set; }
    public VerificationStatus Status { get; private set; }
    public string Reason { get; private set; }
    public DateTimeOffset DecidedAtUtc { get; private set; }
    public Guid? DecidedByApplicationUserId { get; private set; }

    public EvidenceLevel EvidenceLevel => Status switch
    {
        VerificationStatus.Basic => EvidenceLevel.Basic,
        VerificationStatus.Verified => EvidenceLevel.Verified,
        VerificationStatus.Audited => EvidenceLevel.Audited,
        _ => EvidenceLevel.None
    };
}
