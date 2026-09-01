using AgeNexus.Domain.Common;

namespace AgeNexus.Domain.Players;

public sealed class PlayerFavoriteFaction
{
    private PlayerFavoriteFaction()
    {
    }

    public PlayerFavoriteFaction(Guid id, Guid playerProfileId, Guid factionId, int priority)
    {
        if (id == Guid.Empty || playerProfileId == Guid.Empty || factionId == Guid.Empty || priority is < 1 or > 5)
        {
            throw new DomainRuleException("Favorite faction requires ids and priority between 1 and 5.");
        }

        Id = id;
        PlayerProfileId = playerProfileId;
        FactionId = factionId;
        Priority = priority;
    }

    public Guid Id { get; private set; }
    public Guid PlayerProfileId { get; private set; }
    public Guid FactionId { get; private set; }
    public int Priority { get; private set; }
}
