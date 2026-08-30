using AgeNexus.Domain.Common;

namespace AgeNexus.Domain.Players;

public sealed class PlayerProfile
{
    private PlayerProfile()
    {
        DisplayName = null!;
    }

    public PlayerProfile(Guid id, string displayName, Guid? applicationUserId = null)
    {
        if (id == Guid.Empty)
        {
            throw new DomainRuleException("Player profile id is required.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new DomainRuleException("Display name is required.");
        }

        Id = id;
        DisplayName = displayName.Trim();
        ApplicationUserId = applicationUserId;
    }

    public Guid Id { get; private set; }
    public string DisplayName { get; private set; }
    public Guid? ApplicationUserId { get; private set; }
    public bool HasUserAccount => ApplicationUserId.HasValue;

    public void LinkToUser(Guid applicationUserId)
    {
        if (applicationUserId == Guid.Empty)
        {
            throw new DomainRuleException("Application user id is required.");
        }

        if (ApplicationUserId.HasValue && ApplicationUserId != applicationUserId)
        {
            throw new DomainRuleException("Player profile is already linked to another user.");
        }

        ApplicationUserId = applicationUserId;
    }
}
