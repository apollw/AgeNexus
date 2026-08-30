using AgeNexus.Domain.Common;

namespace AgeNexus.Domain.Matches;

public sealed class MatchParticipant
{
    private MatchParticipant()
    {
    }

    private MatchParticipant(
        Guid id,
        ParticipantType type,
        Guid? playerProfileId,
        Guid? aiDifficultyId,
        Guid? factionId,
        FactionSelection factionSelection)
    {
        if (id == Guid.Empty)
        {
            throw new DomainRuleException("Participant id is required.");
        }

        Id = id;
        Type = type;
        PlayerProfileId = playerProfileId;
        AiDifficultyId = aiDifficultyId;
        FactionId = factionId;
        FactionSelection = factionSelection;
    }

    public Guid Id { get; private set; }
    public ParticipantType Type { get; private set; }
    public Guid? PlayerProfileId { get; private set; }
    public Guid? AiDifficultyId { get; private set; }
    public Guid? FactionId { get; private set; }
    public FactionSelection FactionSelection { get; private set; }

    public static MatchParticipant Human(
        Guid id,
        Guid playerProfileId,
        Guid? factionId = null,
        FactionSelection factionSelection = FactionSelection.Unknown)
    {
        if (playerProfileId == Guid.Empty)
        {
            throw new DomainRuleException("A human participant requires a player profile.");
        }

        return new(id, ParticipantType.Human, playerProfileId, null, factionId, factionSelection);
    }

    public static MatchParticipant Ai(
        Guid id,
        Guid aiDifficultyId,
        Guid? factionId = null,
        FactionSelection factionSelection = FactionSelection.Unknown)
    {
        if (aiDifficultyId == Guid.Empty)
        {
            throw new DomainRuleException("An AI participant requires a configured difficulty.");
        }

        return new(id, ParticipantType.ArtificialIntelligence, null, aiDifficultyId, factionId, factionSelection);
    }
}
