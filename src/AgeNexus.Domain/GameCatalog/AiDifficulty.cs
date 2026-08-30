using AgeNexus.Domain.Common;

namespace AgeNexus.Domain.GameCatalog;

public sealed class AiDifficulty
{
    private AiDifficulty()
    {
        Name = null!;
    }

    public AiDifficulty(Guid id, Guid gameEditionId, string name, int internalLevel)
    {
        if (id == Guid.Empty || gameEditionId == Guid.Empty)
        {
            throw new DomainRuleException("Difficulty and game edition ids are required.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainRuleException("Difficulty name is required.");
        }

        if (internalLevel is < 1 or > 5)
        {
            throw new DomainRuleException("AI internal level must be between 1 and 5.");
        }

        Id = id;
        GameEditionId = gameEditionId;
        Name = name.Trim();
        InternalLevel = internalLevel;
    }

    public Guid Id { get; private set; }
    public Guid GameEditionId { get; private set; }
    public string Name { get; private set; }
    public int InternalLevel { get; private set; }
}
