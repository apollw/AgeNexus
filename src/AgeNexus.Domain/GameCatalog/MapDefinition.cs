using AgeNexus.Domain.Common;

namespace AgeNexus.Domain.GameCatalog;

public sealed class MapDefinition
{
    private MapDefinition()
    {
        Name = null!;
        Slug = null!;
    }

    public MapDefinition(Guid id, Guid gameEditionId, string name, string slug)
    {
        if (id == Guid.Empty || gameEditionId == Guid.Empty || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(slug))
        {
            throw new DomainRuleException("Map id, edition id, name and slug are required.");
        }

        Id = id;
        GameEditionId = gameEditionId;
        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
    }

    public Guid Id { get; private set; }
    public Guid GameEditionId { get; private set; }
    public string Name { get; private set; }
    public string Slug { get; private set; }
}
