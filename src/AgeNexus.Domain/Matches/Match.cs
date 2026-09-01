using AgeNexus.Domain.Common;

namespace AgeNexus.Domain.Matches;

public sealed class Match
{
    private readonly List<MatchTeam> _teams = [];

    private Match()
    {
    }

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

    public Guid Id { get; private set; }
    public Guid GameEditionId { get; private set; }
    public Guid CreatedByPlayerProfileId { get; private set; }
    public DateTimeOffset PlayedAtUtc { get; private set; }
    public MatchType Type { get; private set; }
    public MatchNature Nature { get; private set; }
    public MatchStatus Status { get; private set; } = MatchStatus.Draft;
    public Guid? SeasonId { get; private set; }
    public Guid? MapDefinitionId { get; private set; }
    public Guid? GamePatchId { get; private set; }
    public IReadOnlyCollection<MatchTeam> Teams => _teams.AsReadOnly();

    public MatchScoringCategory ScoringCategory => ClassifyScoringCategory();

    public string? HumanFormatLabel
    {
        get
        {
            var humanTeams = _teams.Where(x => x.HumanCount > 0).ToArray();
            if (humanTeams.Length < 2)
            {
                return null;
            }

            return string.Join('x', humanTeams.Select(x => x.HumanCount).OrderDescending());
        }
    }

    public string? CompetitiveFormatLabel
    {
        get
        {
            if (_teams.Count < 2 || _teams.Any(x => x.AiCount > 0 || x.HumanCount == 0))
            {
                return null;
            }

            return HumanFormatLabel;
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

    public void SetCatalogContext(Guid? seasonId, Guid? mapDefinitionId, Guid? gamePatchId)
    {
        EnsureDraft();
        SeasonId = NormalizeOptionalId(seasonId);
        MapDefinitionId = NormalizeOptionalId(mapDefinitionId);
        GamePatchId = NormalizeOptionalId(gamePatchId);
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

    public void SetTeamResult(Guid teamId, TeamResult result)
    {
        EnsureDraft();
        if (result == TeamResult.Undecided)
        {
            throw new DomainRuleException("A declared team result cannot be undecided.");
        }

        var team = _teams.SingleOrDefault(x => x.Id == teamId)
            ?? throw new DomainRuleException("Team does not belong to this match.");
        team.SetResult(result);
    }

    public void Submit()
    {
        EnsureDraft();

        if (_teams.Count < 2 || _teams.Any(x => x.Participants.Count == 0))
        {
            throw new DomainRuleException("A match requires at least two non-empty teams.");
        }

        var category = ClassifyScoringCategory();
        if (Type == MatchType.PlayerVersusPlayer && category != MatchScoringCategory.PurePvp)
        {
            throw new DomainRuleException("A PvP match requires human opponents and cannot contain AI participants.");
        }

        if (Type == MatchType.HumansVersusAi && category != MatchScoringCategory.PurePve)
        {
            throw new DomainRuleException("A humans-versus-AI match requires one human-only team against one AI-only team.");
        }

        if (Type == MatchType.Mixed && category != MatchScoringCategory.HybridPvp)
        {
            throw new DomainRuleException("A mixed match requires human opponents and at least one AI participant.");
        }

        if (Type == MatchType.FreeForAll && _teams.Count < 3)
        {
            throw new DomainRuleException("A free-for-all match requires at least three teams.");
        }

        if (Type != MatchType.FreeForAll && _teams.Count != 2)
        {
            throw new DomainRuleException("Scored matches require exactly two opposing teams.");
        }

        Status = MatchStatus.Submitted;
    }

    public void RequestConfirmation()
    {
        if (Status != MatchStatus.Submitted)
        {
            throw new DomainRuleException("Only a submitted match can request confirmation.");
        }

        Status = MatchStatus.AwaitingConfirmation;
    }

    public void MarkConfirmed()
    {
        if (Status != MatchStatus.AwaitingConfirmation)
        {
            throw new DomainRuleException("Only a match awaiting confirmation can be confirmed.");
        }

        Status = MatchStatus.Confirmed;
    }

    public void MarkDisputed()
    {
        if (Status is MatchStatus.Draft or MatchStatus.Validated or MatchStatus.Voided)
        {
            throw new DomainRuleException("This match cannot be disputed in its current state.");
        }

        Status = MatchStatus.Disputed;
    }

    public void Validate()
    {
        if (Status != MatchStatus.Confirmed)
        {
            throw new DomainRuleException("Only a confirmed match can be validated.");
        }

        if (!HasCoherentResult())
        {
            throw new DomainRuleException("A validated match requires coherent team results.");
        }

        Status = MatchStatus.Validated;
    }

    public void Void()
    {
        if (Status == MatchStatus.Draft || Status == MatchStatus.Voided)
        {
            throw new DomainRuleException("This match cannot be voided in its current state.");
        }

        Status = MatchStatus.Voided;
    }

    private bool HasCoherentResult()
    {
        var results = _teams.Select(x => x.Result).ToArray();
        if (results.Any(x => x == TeamResult.Undecided))
        {
            return false;
        }

        if (results.All(x => x == TeamResult.Draw))
        {
            return true;
        }

        return results.Count(x => x == TeamResult.Victory) == 1 &&
               results.Count(x => x == TeamResult.Defeat) == results.Length - 1;
    }

    private MatchScoringCategory ClassifyScoringCategory()
    {
        if (_teams.Count < 2 || _teams.Any(x => x.Participants.Count == 0))
        {
            return MatchScoringCategory.Ineligible;
        }

        var teamsWithHumans = _teams.Count(x => x.HumanCount > 0);
        var teamsWithAi = _teams.Count(x => x.AiCount > 0);
        var totalAi = _teams.Sum(x => x.AiCount);

        if (teamsWithHumans >= 2 && totalAi == 0)
        {
            return MatchScoringCategory.PurePvp;
        }

        if (teamsWithHumans >= 2 && totalAi > 0)
        {
            return MatchScoringCategory.HybridPvp;
        }

        if (_teams.Count == 2 && teamsWithHumans == 1 && teamsWithAi == 1 &&
            _teams.All(x => x.HumanCount == 0 || x.AiCount == 0))
        {
            return MatchScoringCategory.PurePve;
        }

        return MatchScoringCategory.Ineligible;
    }

    private void EnsureDraft()
    {
        if (Status != MatchStatus.Draft)
        {
            throw new DomainRuleException("Match composition can only change while it is a draft.");
        }
    }

    private static Guid? NormalizeOptionalId(Guid? value) =>
        value == Guid.Empty ? throw new DomainRuleException("Optional catalog ids cannot be empty.") : value;
}
