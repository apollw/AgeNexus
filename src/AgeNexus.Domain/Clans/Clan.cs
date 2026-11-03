using AgeNexus.Domain.Common;

namespace AgeNexus.Domain.Clans;

public sealed class Clan
{
    private Clan()
    {
        Name = null!;
        Tag = null!;
    }

    public Clan(Guid id, string name, string tag, Guid createdByPlayerProfileId, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty || createdByPlayerProfileId == Guid.Empty)
        {
            throw new DomainRuleException("Clan and founder ids are required.");
        }

        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
        {
            throw new DomainRuleException("Clan name must contain between 1 and 100 characters.");
        }

        if (string.IsNullOrWhiteSpace(tag) || tag.Trim().Length is < 2 or > 8)
        {
            throw new DomainRuleException("Clan tag must contain between 2 and 8 characters.");
        }

        if (createdAtUtc.Offset != TimeSpan.Zero)
        {
            throw new DomainRuleException("Clan creation time must be provided in UTC.");
        }

        Id = id;
        Name = name.Trim();
        Tag = tag.Trim().ToUpperInvariant();
        CreatedByPlayerProfileId = createdByPlayerProfileId;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Tag { get; private set; }
    public string? Description { get; private set; }
    public string? Motto { get; private set; }
    public string? ImageUrl { get; private set; }
    public bool IsRecruiting { get; private set; }
    public Guid CreatedByPlayerProfileId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public void Update(string name, string tag, string? description, string? motto, bool isRecruiting)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100 ||
            string.IsNullOrWhiteSpace(tag) || tag.Trim().Length is < 2 or > 8 ||
            description?.Trim().Length > 500 || motto?.Trim().Length > 120)
        {
            throw new DomainRuleException("Invalid clan profile.");
        }
        Name = name.Trim();
        Tag = tag.Trim().ToUpperInvariant();
        Description = Normalize(description);
        Motto = Normalize(motto);
        IsRecruiting = isRecruiting;
    }

    public void UpdateImage(string? imageUrl) => ImageUrl = Normalize(imageUrl);

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
