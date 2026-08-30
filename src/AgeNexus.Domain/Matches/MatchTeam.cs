using AgeNexus.Domain.Common;

namespace AgeNexus.Domain.Matches;

public sealed class MatchTeam
{
    private readonly List<MatchParticipant> _participants = [];

    internal MatchTeam(Guid id, int position)
    {
        if (id == Guid.Empty)
        {
            throw new DomainRuleException("Team id is required.");
        }

        Id = id;
        Position = position;
    }

    public Guid Id { get; }
    public int Position { get; }
    public TeamResult Result { get; private set; } = TeamResult.Undecided;
    public IReadOnlyCollection<MatchParticipant> Participants => _participants.AsReadOnly();
    public int HumanCount => _participants.Count(x => x.Type == ParticipantType.Human);
    public int AiCount => _participants.Count(x => x.Type == ParticipantType.ArtificialIntelligence);

    internal void AddParticipant(MatchParticipant participant)
    {
        ArgumentNullException.ThrowIfNull(participant);

        if (_participants.Any(x => x.Id == participant.Id))
        {
            throw new DomainRuleException("Participant is already in this team.");
        }

        _participants.Add(participant);
    }

    public void SetResult(TeamResult result) => Result = result;
}
