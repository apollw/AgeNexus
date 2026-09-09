using AgeNexus.Domain.Common;

namespace AgeNexus.Domain.Players;

public enum PlayerProfileClaimStatus
{
    Pending,
    Approved,
    Rejected
}

public sealed class PlayerProfileClaim
{
    private PlayerProfileClaim()
    {
    }

    public PlayerProfileClaim(
        Guid id,
        Guid applicationUserId,
        Guid playerProfileId,
        DateTimeOffset requestedAtUtc)
    {
        if (id == Guid.Empty || applicationUserId == Guid.Empty || playerProfileId == Guid.Empty ||
            requestedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new DomainRuleException("A profile claim requires ids and an UTC request time.");
        }

        Id = id;
        ApplicationUserId = applicationUserId;
        PlayerProfileId = playerProfileId;
        RequestedAtUtc = requestedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ApplicationUserId { get; private set; }
    public Guid PlayerProfileId { get; private set; }
    public PlayerProfileClaimStatus Status { get; private set; } = PlayerProfileClaimStatus.Pending;
    public DateTimeOffset RequestedAtUtc { get; private set; }
    public DateTimeOffset? DecidedAtUtc { get; private set; }
    public Guid? DecidedByApplicationUserId { get; private set; }

    public void Approve(Guid decidedByApplicationUserId, DateTimeOffset decidedAtUtc) =>
        Decide(PlayerProfileClaimStatus.Approved, decidedByApplicationUserId, decidedAtUtc);

    public void Reject(Guid decidedByApplicationUserId, DateTimeOffset decidedAtUtc) =>
        Decide(PlayerProfileClaimStatus.Rejected, decidedByApplicationUserId, decidedAtUtc);

    private void Decide(
        PlayerProfileClaimStatus status,
        Guid decidedByApplicationUserId,
        DateTimeOffset decidedAtUtc)
    {
        if (Status != PlayerProfileClaimStatus.Pending || decidedByApplicationUserId == Guid.Empty ||
            decidedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new DomainRuleException("Only a pending profile claim can be decided.");
        }

        Status = status;
        DecidedByApplicationUserId = decidedByApplicationUserId;
        DecidedAtUtc = decidedAtUtc;
    }
}
