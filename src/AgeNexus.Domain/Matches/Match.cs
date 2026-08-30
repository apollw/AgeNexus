using AgeNexus.Domain.Common;

namespace AgeNexus.Domain.Matches;

public sealed class Match
{
    private readonly List<MatchTeam> _teams = [];

    public Match(
        Guid id,
        Guid gameEditionId,
        Guid createdByPlayerProfileId,
        DateTimeOffset playedAtUtc,
        MatchType type,
        MatchNature nature)
    {
        if (id == Guid.Empty || gameEditionId == Guid.Empty || createdByPlayerProfileId == Guid.Empty)
        {
            throw new DomainRuleException("Match, edition and author ids are required.");
        }

        if (playedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new DomainRuleException("Match time must be provided in UTC.");
        }

        Id = id;
        GameEditionId = gameEditionId;
        CreatedByPlayerProfileId = createdByPlayerProfileId;
        PlayedAtUtc = playedAtUtc;
        Type = type;
        Nature = nature;
    }

    public Guid Id { get; }
    public Guid GameEditionId { get; }
    public Guid CreatedByPlayerProfileId { get; }
    public DateTimeOffset PlayedAtUtc { get; }
    public MatchType Type { get; }
    public MatchNature Nature { get; }
    public MatchStatus Status { get; private set; } = MatchStatus.Draft;
    public IReadOnlyCollection<MatchTeam> Teams => _teams.AsReadOnly();

    public string? CompetitiveFormatLabel
    {
        get
        {
            if (_teams.Count < 2 || _teams.Any(x => x.AiCount > 0 || x.HumanCount == 0))
            {
                return null;
            }

            return string.Join('x', _teams.Select(x => x.HumanCount).OrderDescending());
        }
    }

    public MatchTeam AddTeam(Guid teamId)
    {
        EnsureDraft();

        if (_teams.Any(x => x.Id == teamId))
        {
            throw new DomainRuleException("Team is already in this match.");
        }

        var team = new MatchTeam(teamId, _teams.Count + 1);
        _teams.Add(team);
        return team;
    }

    public void AddParticipant(Guid teamId, MatchParticipant participant)
    {
        EnsureDraft();
        ArgumentNullException.ThrowIfNull(participant);

        if (_teams.SelectMany(x => x.Participants).Any(x => x.Id == participant.Id))
        {
            throw new DomainRuleException("Participant id must be unique within a match.");
        }

        if (participant.PlayerProfileId.HasValue &&
            _teams.SelectMany(x => x.Participants).Any(x => x.PlayerProfileId == participant.PlayerProfileId))
        {
            throw new DomainRuleException("A player profile cannot participate twice in a match.");
        }

        var team = _teams.SingleOrDefault(x => x.Id == teamId)
            ?? throw new DomainRuleException("Team does not belong to this match.");
        team.AddParticipant(participant);
    }

    public void Submit()
    {
        EnsureDraft();

        if (_teams.Count < 2 || _teams.Any(x => x.Participants.Count == 0))
        {
            throw new DomainRuleException("A match requires at least two non-empty teams.");
        }

        var humanCount = _teams.Sum(x => x.HumanCount);
        var aiCount = _teams.Sum(x => x.AiCount);

        if (Type == MatchType.PlayerVersusPlayer && (humanCount == 0 || aiCount > 0))
        {
            throw new DomainRuleException("A PvP match can contain only human participants.");
        }

        if (Type == MatchType.HumansVersusAi && (humanCount == 0 || aiCount == 0))
        {
            throw new DomainRuleException("A humans-versus-AI match requires humans and AI.");
        }

        if (Type == MatchType.FreeForAll && _teams.Count < 3)
        {
            throw new DomainRuleException("A free-for-all match requires at least three teams.");
        }

        Status = MatchStatus.Submitted;
    }

    private void EnsureDraft()
    {
        if (Status != MatchStatus.Draft)
        {
            throw new DomainRuleException("Match composition can only change while it is a draft.");
        }
    }
}
