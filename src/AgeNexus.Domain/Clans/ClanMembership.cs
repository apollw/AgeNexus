using AgeNexus.Domain.Common;

namespace AgeNexus.Domain.Clans;

public enum ClanRole
{
    Member,
    Officer,
    Leader
}

public sealed class ClanMembership
{
    private ClanMembership()
    {
    }

    public ClanMembership(
        Guid id,
        Guid clanId,
        Guid playerProfileId,
        ClanRole role,
        DateTimeOffset startedAtUtc)
    {
        if (id == Guid.Empty || clanId == Guid.Empty || playerProfileId == Guid.Empty)
        {
            throw new DomainRuleException("Membership, clan and player ids are required.");
        }

        if (startedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new DomainRuleException("Membership start must be provided in UTC.");
        }

        Id = id;
        ClanId = clanId;
        PlayerProfileId = playerProfileId;
        Role = role;
        StartedAtUtc = startedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ClanId { get; private set; }
    public Guid PlayerProfileId { get; private set; }
    public ClanRole Role { get; private set; }
    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset? EndedAtUtc { get; private set; }

    public bool WasActiveAt(DateTimeOffset instantUtc) =>
        instantUtc >= StartedAtUtc && (!EndedAtUtc.HasValue || instantUtc < EndedAtUtc.Value);

    public void End(DateTimeOffset endedAtUtc)
    {
        if (endedAtUtc.Offset != TimeSpan.Zero || endedAtUtc <= StartedAtUtc || EndedAtUtc.HasValue)
        {
            throw new DomainRuleException("Membership end must be a valid UTC instant after its start.");
        }

        EndedAtUtc = endedAtUtc;
    }
}
