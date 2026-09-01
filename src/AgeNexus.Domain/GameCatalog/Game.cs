using AgeNexus.Domain.Common;

namespace AgeNexus.Domain.GameCatalog;

public sealed class Game
{
    private Game()
    {
        Name = null!;
        Slug = null!;
    }

    public Game(Guid id, string name, string slug)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(slug))
        {
            throw new DomainRuleException("Game id, name and slug are required.");
        }

        Id = id;
        Name = name.Trim();
        Slug = NormalizeSlug(slug);
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Slug { get; private set; }

    private static string NormalizeSlug(string value)
    {
        var slug = value.Trim().ToLowerInvariant();
        if (slug.Length > 80 || slug.Any(x => !char.IsLetterOrDigit(x) && x != '-'))
        {
            throw new DomainRuleException("Game slug accepts only letters, numbers and hyphens.");
        }

        return slug;
    }
}
