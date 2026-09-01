using AgeNexus.Domain.Common;

namespace AgeNexus.Domain.GameCatalog;

public sealed class GamePatch
{
    private GamePatch()
    {
        Name = null!;
    }

    public GamePatch(Guid id, Guid gameEditionId, string name, DateTimeOffset effectiveFromUtc)
    {
        if (id == Guid.Empty || gameEditionId == Guid.Empty || string.IsNullOrWhiteSpace(name) ||
            effectiveFromUtc.Offset != TimeSpan.Zero)
        {
            throw new DomainRuleException("Patch requires ids, name and UTC effective time.");
        }

        Id = id;
        GameEditionId = gameEditionId;
        Name = name.Trim();
        EffectiveFromUtc = effectiveFromUtc;
    }

    public Guid Id { get; private set; }
    public Guid GameEditionId { get; private set; }
    public string Name { get; private set; }
    public DateTimeOffset EffectiveFromUtc { get; private set; }
    public DateTimeOffset? EffectiveToUtc { get; private set; }

    public void CloseAt(DateTimeOffset instantUtc)
    {
        if (instantUtc.Offset != TimeSpan.Zero || instantUtc <= EffectiveFromUtc)
        {
            throw new DomainRuleException("Patch end must be a UTC instant after its start.");
        }

        EffectiveToUtc = instantUtc;
    }
}
