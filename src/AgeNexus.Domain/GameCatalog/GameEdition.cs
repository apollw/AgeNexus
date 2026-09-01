using AgeNexus.Domain.Common;

namespace AgeNexus.Domain.GameCatalog;

public sealed class GameEdition
{
    private GameEdition()
    {
        Name = null!;
        Slug = null!;
    }

    public GameEdition(Guid id, Guid gameId, string name, string slug)
    {
        if (id == Guid.Empty || gameId == Guid.Empty || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(slug))
        {
            throw new DomainRuleException("Edition id, game id, name and slug are required.");
        }

        Id = id;
        GameId = gameId;
        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
    }

    public Guid Id { get; private set; }
    public Guid GameId { get; private set; }
    public string Name { get; private set; }
    public string Slug { get; private set; }
    public bool IsActive { get; private set; } = true;

    public void Deactivate() => IsActive = false;
}
