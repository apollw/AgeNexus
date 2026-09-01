using AgeNexus.Domain.Common;

namespace AgeNexus.Domain.GameCatalog;

public sealed class Faction
{
    private Faction()
    {
        Name = null!;
        Slug = null!;
    }

    public Faction(Guid id, Guid gameEditionId, string name, string slug, string? imageUrl = null)
    {
        if (id == Guid.Empty || gameEditionId == Guid.Empty || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(slug))
        {
            throw new DomainRuleException("Faction id, edition id, name and slug are required.");
        }

        Id = id;
        GameEditionId = gameEditionId;
        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        ImageUrl = NormalizeHttpUrl(imageUrl);
    }

    public Guid Id { get; private set; }
    public Guid GameEditionId { get; private set; }
    public string Name { get; private set; }
    public string Slug { get; private set; }
    public string? ImageUrl { get; private set; }

    private static string? NormalizeHttpUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new DomainRuleException("Faction image URL must be absolute HTTP or HTTPS.");
        }

        return normalized;
    }
}
