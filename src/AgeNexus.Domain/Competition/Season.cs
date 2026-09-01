using AgeNexus.Domain.Common;

namespace AgeNexus.Domain.Competition;

public sealed class Season
{
    private Season()
    {
        Name = null!;
    }

    public Season(
        Guid id,
        Guid gameEditionId,
        string name,
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc)
    {
        if (id == Guid.Empty || gameEditionId == Guid.Empty || string.IsNullOrWhiteSpace(name) ||
            startsAtUtc.Offset != TimeSpan.Zero || endsAtUtc.Offset != TimeSpan.Zero || endsAtUtc <= startsAtUtc)
        {
            throw new DomainRuleException("Season requires ids, name and a valid UTC interval.");
        }

        Id = id;
        GameEditionId = gameEditionId;
        Name = name.Trim();
        StartsAtUtc = startsAtUtc;
        EndsAtUtc = endsAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid GameEditionId { get; private set; }
    public string Name { get; private set; }
    public DateTimeOffset StartsAtUtc { get; private set; }
    public DateTimeOffset EndsAtUtc { get; private set; }

    public bool Contains(DateTimeOffset instantUtc) => instantUtc >= StartsAtUtc && instantUtc < EndsAtUtc;
}
