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

        if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > 100)
        {
            throw new DomainRuleException("Display name must contain between 1 and 100 characters.");
        }

        Id = id;
        DisplayName = displayName.Trim();
        ApplicationUserId = applicationUserId;
    }

    public Guid Id { get; private set; }
    public string DisplayName { get; private set; }
    public Guid? ApplicationUserId { get; private set; }
    public string? Bio { get; private set; }
    public string? Location { get; private set; }
    public string? AvatarUrl { get; private set; }
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

    public void UpdatePublicProfile(string displayName, string? bio, string? location, string? avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > 100)
        {
            throw new DomainRuleException("Display name must contain between 1 and 100 characters.");
        }

        if (bio?.Trim().Length > 500)
        {
            throw new DomainRuleException("Bio cannot exceed 500 characters.");
        }

        if (location?.Trim().Length > 100)
        {
            throw new DomainRuleException("Location cannot exceed 100 characters.");
        }

        var normalizedAvatarUrl = NormalizeOptional(avatarUrl);
        if (normalizedAvatarUrl is not null &&
            (!Uri.TryCreate(normalizedAvatarUrl, UriKind.Absolute, out var avatarUri) ||
             (avatarUri.Scheme != Uri.UriSchemeHttps && avatarUri.Scheme != Uri.UriSchemeHttp)))
        {
            throw new DomainRuleException("Avatar URL must be an absolute HTTP or HTTPS URL.");
        }

        if (normalizedAvatarUrl?.Length > 500)
        {
            throw new DomainRuleException("Avatar URL cannot exceed 500 characters.");
        }

        DisplayName = displayName.Trim();
        Bio = NormalizeOptional(bio);
        Location = NormalizeOptional(location);
        AvatarUrl = normalizedAvatarUrl;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

