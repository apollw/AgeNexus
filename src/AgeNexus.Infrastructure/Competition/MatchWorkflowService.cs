using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgeNexus.Application.Matches;
using AgeNexus.Application.Ratings;
using AgeNexus.Domain.Common;
using AgeNexus.Domain.Competition;
using AgeNexus.Domain.EvidenceAndModeration;
using AgeNexus.Domain.Matches;
using AgeNexus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using DomainMatchType = AgeNexus.Domain.Matches.MatchType;

namespace AgeNexus.Infrastructure.Competition;

public sealed class MatchWorkflowService(
    AgeNexusDbContext database,
    IRatingCalculator ratingCalculator,
    ICareerPointCalculator careerPointCalculator,
    IPvePointCalculator pvePointCalculator,
    ScoringRuleSet rules) : IMatchWorkflowService
{
    public async Task<MatchWorkflowResult> RegisterAsync(
        RegisterMatchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var matchId = Guid.NewGuid();
        try
        {
            var match = new Match(
                matchId,
                request.GameEditionId,
                request.CreatedByPlayerProfileId,
                request.PlayedAtUtc,
                request.Type,
                request.Nature);
            match.SetCatalogContext(request.SeasonId, request.MapDefinitionId, request.GamePatchId);

            foreach (var requestedTeam in request.Teams)
            {
                var team = match.AddTeam(Guid.NewGuid());
                foreach (var requestedParticipant in requestedTeam.Participants)
                {
                    var participant = requestedParticipant.Type == ParticipantType.Human
                        ? MatchParticipant.Human(
                            Guid.NewGuid(),
                            requestedParticipant.PlayerProfileId ?? Guid.Empty,
                            requestedParticipant.FactionId,
                            requestedParticipant.FactionSelection)
                        : MatchParticipant.Ai(
                            Guid.NewGuid(),
                            requestedParticipant.AiDifficultyId ?? Guid.Empty,
                            requestedParticipant.FactionId,
                            requestedParticipant.FactionSelection);
                    match.AddParticipant(team.Id, participant);
                }

                match.SetTeamResult(team.Id, requestedTeam.Result);
            }

            match.Submit();
            match.RequestConfirmation();
            database.Matches.Add(match);
            await database.SaveChangesAsync(cancellationToken);
            return MatchWorkflowResult.Success(matchId);
        }
        catch (DomainRuleException)
        {
            return MatchWorkflowResult.Failure(matchId, "InvalidMatch");
        }
    }

    public async Task<MatchWorkflowResult> ConfirmAsync(
        Guid matchId,
        Guid playerProfileId,
        ConfirmationDecision decision,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        var match = await LoadMatchAsync(matchId, cancellationToken);
        if (match is null)
        {
            return MatchWorkflowResult.Failure(matchId, "MatchNotFound");
        }

        var humanIds = match.Teams.SelectMany(x => x.Participants)
            .Where(x => x.Type == ParticipantType.Human)
            .Select(x => x.PlayerProfileId!.Value)
            .Distinct()
            .ToArray();
        if (!humanIds.Contains(playerProfileId))
        {
            return MatchWorkflowResult.Failure(matchId, "PlayerNotInMatch");
        }

        var existing = await database.MatchConfirmations.SingleOrDefaultAsync(
            x => x.MatchId == matchId && x.PlayerProfileId == playerProfileId,
            cancellationToken);
        if (existing is not null)
        {
            return existing.Decision == decision
                ? MatchWorkflowResult.Success(matchId, alreadyApplied: true)
                : MatchWorkflowResult.Failure(matchId, "ConfirmationAlreadyRecorded");
        }

        try
        {
            database.MatchConfirmations.Add(new MatchConfirmation(
                Guid.NewGuid(),
                matchId,
                playerProfileId,
                decision,
                DateTimeOffset.UtcNow,
                comment));

            if (decision == ConfirmationDecision.Contested)
            {
                match.MarkDisputed();
            }
            else
            {
                var confirmedIds = await database.MatchConfirmations
                    .Where(x => x.MatchId == matchId && x.Decision == ConfirmationDecision.Confirmed)
                    .Select(x => x.PlayerProfileId)
                    .ToListAsync(cancellationToken);
                confirmedIds.Add(playerProfileId);
                if (humanIds.All(confirmedIds.Contains))
                {
                    match.MarkConfirmed();
                }
            }

            await database.SaveChangesAsync(cancellationToken);
            return MatchWorkflowResult.Success(matchId);
        }
        catch (DomainRuleException)
        {
            return MatchWorkflowResult.Failure(matchId, "InvalidMatchState");
        }
    }

    public async Task<MatchWorkflowResult> DecideEvidenceAsync(
        Guid matchId,
        VerificationStatus status,
        string reason,
        Guid? decidedByApplicationUserId,
        CancellationToken cancellationToken = default)
    {
        var match = await LoadMatchAsync(matchId, cancellationToken);
        if (match is null)
        {
            return MatchWorkflowResult.Failure(matchId, "MatchNotFound");
        }

        try
        {
            database.VerificationDecisions.Add(new VerificationDecision(
                Guid.NewGuid(),
                matchId,
                status,
                reason,
                DateTimeOffset.UtcNow,
                decidedByApplicationUserId));
            if (status == VerificationStatus.Contested && match.Status != MatchStatus.Disputed)
            {
                match.MarkDisputed();
            }

            await database.SaveChangesAsync(cancellationToken);
            return MatchWorkflowResult.Success(matchId);
        }
        catch (DomainRuleException)
        {
            return MatchWorkflowResult.Failure(matchId, "InvalidVerificationDecision");
        }
    }

    public async Task<MatchWorkflowResult> ValidateAsync(
        Guid matchId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var match = await LoadMatchAsync(matchId, cancellationToken);
        if (match is null)
        {
            return MatchWorkflowResult.Failure(matchId, "MatchNotFound");
        }

        if (match.Status == MatchStatus.Validated)
        {
            return MatchWorkflowResult.Success(matchId, alreadyApplied: true);
        }

        try
        {
            if (match.Type != DomainMatchType.FreeForAll)
            {
                if (match.ScoringCategory is MatchScoringCategory.PurePvp or MatchScoringCategory.HybridPvp)
                {
                    await AddPvpEventsAsync(match, cancellationToken);
                }
                else if (match.ScoringCategory == MatchScoringCategory.PurePve)
                {
                    await AddPveEventsAsync(match, cancellationToken);
                }
            }

            match.Validate();
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return MatchWorkflowResult.Success(matchId);
        }
        catch (DomainRuleException)
        {
            return MatchWorkflowResult.Failure(matchId, "InvalidMatchState");
        }
    }

    public async Task<MatchWorkflowResult> VoidAsync(
        Guid matchId,
        Guid changedByApplicationUserId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var match = await LoadMatchAsync(matchId, cancellationToken);
        if (match is null)
        {
            return MatchWorkflowResult.Failure(matchId, "MatchNotFound");
        }

        if (match.Status == MatchStatus.Voided)
        {
            return MatchWorkflowResult.Success(matchId, alreadyApplied: true);
        }

        try
        {
            if (match.Status == MatchStatus.Validated)
            {
                var ratingAwards = await database.RatingEvents
                    .Where(x => x.MatchId == matchId && x.Kind == ScoringEventKind.Award)
                    .ToListAsync(cancellationToken);
                foreach (var award in ratingAwards)
                {
                    database.RatingEvents.Add(new RatingEvent(
                        Guid.NewGuid(),
                        matchId,
                        award.BeneficiaryId,
                        award.SeasonId,
                        award.Scope,
                        -award.Delta,
                        award.RuleVersion,
                        JsonSerializer.Serialize(new { ReversalReason = reason, OriginalEventId = award.Id }),
                        DateTimeOffset.UtcNow,
                        ScoringEventKind.Reversal,
                        award.Id));
                }

                var pointAwards = await database.PointEvents
                    .Where(x => x.MatchId == matchId && x.Kind == ScoringEventKind.Award)
                    .ToListAsync(cancellationToken);
                foreach (var award in pointAwards)
                {
                    database.PointEvents.Add(new PointEvent(
                        Guid.NewGuid(),
                        matchId,
                        award.BeneficiaryId,
                        award.SeasonId,
                        award.Scope,
                        -award.Points,
                        award.RuleVersion,
                        JsonSerializer.Serialize(new { ReversalReason = reason, OriginalEventId = award.Id }),
                        DateTimeOffset.UtcNow,
                        award.SourceKey,
                        award.EvidenceLevel,
                        ScoringEventKind.Reversal,
                        award.Id));
                }
            }

            database.MatchRevisions.Add(new MatchRevision(
                Guid.NewGuid(),
                matchId,
                changedByApplicationUserId,
                reason,
                JsonSerializer.Serialize(new { match.Status, match.Type, match.PlayedAtUtc }),
                DateTimeOffset.UtcNow));
            match.Void();
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return MatchWorkflowResult.Success(matchId);
        }
        catch (DomainRuleException)
        {
            return MatchWorkflowResult.Failure(matchId, "InvalidMatchState");
        }
    }

    private async Task AddPvpEventsAsync(Match match, CancellationToken cancellationToken)
    {
        var teams = match.Teams.ToArray();
        if (teams.Length != 2)
        {
            throw new DomainRuleException("Competitive PvP scoring requires exactly two teams.");
        }

        var humanIds = teams.SelectMany(x => x.Participants)
            .Where(x => x.Type == ParticipantType.Human)
            .Select(x => x.PlayerProfileId!.Value)
            .Distinct()
            .ToArray();
        var ratingSums = await database.RatingEvents
            .Where(x => x.Scope == RatingScopeKind.GeneralCompetitive && humanIds.Contains(x.BeneficiaryId))
            .GroupBy(x => x.BeneficiaryId)
            .Select(x => new { PlayerId = x.Key, Delta = x.Sum(e => e.Delta) })
            .ToDictionaryAsync(x => x.PlayerId, x => x.Delta, cancellationToken);
        var matchCounts = await database.RatingEvents
            .Where(x => x.Scope == RatingScopeKind.GeneralCompetitive &&
                        x.Kind == ScoringEventKind.Award && humanIds.Contains(x.BeneficiaryId))
            .GroupBy(x => x.BeneficiaryId)
            .Select(x => new { PlayerId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.PlayerId, x => x.Count, cancellationToken);
        var aiIds = teams.SelectMany(x => x.Participants)
            .Where(x => x.Type == ParticipantType.ArtificialIntelligence)
            .Select(x => x.AiDifficultyId!.Value)
            .Distinct()
            .ToArray();
        var aiLevels = await database.AiDifficulties
            .Where(x => aiIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.InternalLevel, cancellationToken);

        var ratedTeams = teams.Select(team => new RatedTeam(
            team.Id,
            ToNumericResult(team.Result),
            team.Participants.Select(participant => participant.Type == ParticipantType.Human
                ? new RatedParticipant(
                    participant.PlayerProfileId,
                    ScoringRuleSet.InitialRating + ratingSums.GetValueOrDefault(participant.PlayerProfileId!.Value),
                    matchCounts.GetValueOrDefault(participant.PlayerProfileId.Value))
                : new RatedParticipant(
                    null,
                    rules.GetAiRule(aiLevels[participant.AiDifficultyId!.Value]).EquivalentRating,
                    0,
                    IsAi: true)).ToArray())).ToArray();
        var largestTeam = teams.Max(x => x.HumanCount);
        var asymmetric = teams.Select(x => x.HumanCount).Distinct().Count() > 1;
        var modalityWeight = rules.GetModalityWeight(largestTeam, asymmetric);
        var aiCount = teams.Sum(x => x.AiCount);
        var totalParticipants = teams.Sum(x => x.Participants.Count);
        var categoryFactor = match.ScoringCategory == MatchScoringCategory.HybridPvp
            ? rules.GetHybridRatingFactor(aiCount, totalParticipants)
            : 1m;
        var rating = ratingCalculator.Calculate(new RatingCalculationRequest(ratedTeams, modalityWeight, categoryFactor));
        var details = JsonSerializer.Serialize(new
        {
            match.ScoringCategory,
            Format = match.HumanFormatLabel,
            ModalityWeight = modalityWeight,
            CategoryFactor = categoryFactor,
            RuleVersion = rules.Version
        });
        foreach (var delta in rating.Deltas)
        {
            database.RatingEvents.Add(new RatingEvent(
                Guid.NewGuid(),
                match.Id,
                delta.PlayerId,
                match.SeasonId,
                RatingScopeKind.GeneralCompetitive,
                delta.Delta,
                rules.Version,
                details,
                DateTimeOffset.UtcNow));
        }

        var strengths = ratedTeams.ToDictionary(
            x => x.TeamId,
            x => rules.CalculateEffectiveTeamStrength(x.Participants.Select(p => p.CurrentRating).ToArray()));
        var careerTeams = teams.Select((team, index) => new CareerTeam(
            team.Participants.Where(x => x.Type == ParticipantType.Human).Select(x => x.PlayerProfileId!.Value).ToArray(),
            team.HumanCount,
            teams[1 - index].HumanCount,
            ToScoringResult(team.Result),
            rules.CalculateExpectedScore(strengths[team.Id], strengths[teams[1 - index].Id]))).ToArray();
        var career = careerPointCalculator.Calculate(new CareerPointCalculationRequest(
            match.ScoringCategory,
            largestTeam,
            careerTeams));
        foreach (var award in career.Awards)
        {
            database.PointEvents.Add(new PointEvent(
                Guid.NewGuid(),
                match.Id,
                award.PlayerId,
                match.SeasonId,
                PointScopeKind.Career,
                award.Points,
                rules.Version,
                details,
                DateTimeOffset.UtcNow));
        }

        await AddTeamLineupEventsAsync(match, teams, modalityWeight, categoryFactor, details, cancellationToken);
    }

    private async Task AddTeamLineupEventsAsync(
        Match match,
        IReadOnlyList<MatchTeam> teams,
        decimal modalityWeight,
        decimal categoryFactor,
        string details,
        CancellationToken cancellationToken)
    {
        var lineups = new List<(TeamLineup Lineup, MatchTeam Team)>();
        foreach (var team in teams)
        {
            var playerIds = team.Participants
                .Where(x => x.Type == ParticipantType.Human)
                .Select(x => x.PlayerProfileId!.Value)
                .ToArray();
            var candidate = TeamLineup.Create(match.GameEditionId, playerIds, DateTimeOffset.UtcNow);
            var lineup = await database.TeamLineups.SingleOrDefaultAsync(
                x => x.Id == candidate.Id,
                cancellationToken);
            if (lineup is null)
            {
                lineup = candidate;
                database.TeamLineups.Add(lineup);
            }

            lineups.Add((lineup, team));
        }

        var lineupIds = lineups.Select(x => x.Lineup.Id).ToArray();
        var ratingSums = await database.RatingEvents
            .Where(x => x.Scope == RatingScopeKind.TeamLineup && lineupIds.Contains(x.BeneficiaryId))
            .GroupBy(x => x.BeneficiaryId)
            .Select(x => new { LineupId = x.Key, Delta = x.Sum(e => e.Delta) })
            .ToDictionaryAsync(x => x.LineupId, x => x.Delta, cancellationToken);
        var matchCounts = await database.RatingEvents
            .Where(x => x.Scope == RatingScopeKind.TeamLineup && x.Kind == ScoringEventKind.Award &&
                        lineupIds.Contains(x.BeneficiaryId))
            .GroupBy(x => x.BeneficiaryId)
            .Select(x => new { LineupId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.LineupId, x => x.Count, cancellationToken);
        var currentRatings = lineups.ToDictionary(
            x => x.Lineup.Id,
            x => ScoringRuleSet.InitialRating + ratingSums.GetValueOrDefault(x.Lineup.Id));

        for (var index = 0; index < lineups.Count; index++)
        {
            var current = lineups[index];
            var opponent = lineups[1 - index];
            var expected = rules.CalculateExpectedScore(
                currentRatings[current.Lineup.Id],
                currentRatings[opponent.Lineup.Id]);
            var delta = rules.CalculateRatingDelta(
                expected,
                ToScoringResult(current.Team.Result),
                matchCounts.GetValueOrDefault(current.Lineup.Id),
                modalityWeight,
                categoryFactor);
            database.RatingEvents.Add(new RatingEvent(
                Guid.NewGuid(),
                match.Id,
                current.Lineup.Id,
                match.SeasonId,
                RatingScopeKind.TeamLineup,
                delta,
                rules.Version,
                details,
                DateTimeOffset.UtcNow));
        }
    }

    private async Task AddPveEventsAsync(Match match, CancellationToken cancellationToken)
    {
        var humanTeam = match.Teams.Single(x => x.HumanCount > 0);
        var aiTeam = match.Teams.Single(x => x.AiCount > 0);
        var humanIds = humanTeam.Participants.Select(x => x.PlayerProfileId!.Value).ToArray();
        var aiDifficultyIds = aiTeam.Participants.Select(x => x.AiDifficultyId!.Value).ToArray();
        var aiLevels = await database.AiDifficulties
            .Where(x => aiDifficultyIds.Contains(x.Id))
            .Select(x => x.InternalLevel)
            .ToArrayAsync(cancellationToken);
        if (aiLevels.Length != aiDifficultyIds.Length)
        {
            throw new DomainRuleException("Every AI participant requires a configured difficulty.");
        }

        var latestDecision = await database.VerificationDecisions
            .Where(x => x.MatchId == match.Id)
            .OrderByDescending(x => x.DecidedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        var evidenceLevel = latestDecision?.EvidenceLevel ?? EvidenceLevel.None;
        var sourceKey = CreatePveSourceKey(match, aiLevels, humanIds);
        var result = ToScoringResult(humanTeam.Result);

        foreach (var playerId in humanIds)
        {
            var previousWins = await database.PointEvents.CountAsync(
                x => x.BeneficiaryId == playerId && x.SeasonId == match.SeasonId &&
                     x.Scope == PointScopeKind.Pve && x.SourceKey == sourceKey &&
                     x.Kind == ScoringEventKind.Award && x.Points > 0m,
                cancellationToken);
            var basicAwarded = await database.PointEvents
                .Where(x => x.BeneficiaryId == playerId && x.SeasonId == match.SeasonId &&
                            x.Scope == PointScopeKind.Pve && x.EvidenceLevel == EvidenceLevel.Basic)
                .SumAsync(x => (decimal?)x.Points, cancellationToken) ?? 0m;
            var calculation = pvePointCalculator.Calculate(new PvePointCalculationRequest(
                [playerId],
                humanIds.Length,
                aiLevels,
                result,
                previousWins,
                evidenceLevel,
                basicAwarded));
            var award = calculation.Awards.Single();
            var details = JsonSerializer.Serialize(new
            {
                EvidenceLevel = evidenceLevel,
                AiLevels = aiLevels,
                RepetitionIndex = previousWins,
                SourceKey = sourceKey,
                RuleVersion = rules.Version
            });
            database.PointEvents.Add(new PointEvent(
                Guid.NewGuid(),
                match.Id,
                playerId,
                match.SeasonId,
                PointScopeKind.Pve,
                award.PvePoints,
                rules.Version,
                details,
                DateTimeOffset.UtcNow,
                sourceKey,
                evidenceLevel));
            database.PointEvents.Add(new PointEvent(
                Guid.NewGuid(),
                match.Id,
                playerId,
                match.SeasonId,
                PointScopeKind.Career,
                award.CareerPoints,
                rules.Version,
                details,
                DateTimeOffset.UtcNow,
                sourceKey,
                evidenceLevel));
        }
    }

    private Task<Match?> LoadMatchAsync(Guid matchId, CancellationToken cancellationToken) =>
        database.Matches
            .Include(x => x.Teams)
            .ThenInclude(x => x.Participants)
            .SingleOrDefaultAsync(x => x.Id == matchId, cancellationToken);

    private static decimal ToNumericResult(TeamResult result) => result switch
    {
        TeamResult.Victory => 1m,
        TeamResult.Draw => 0.5m,
        TeamResult.Defeat => 0m,
        _ => throw new DomainRuleException("Undecided result cannot be scored.")
    };

    private static ScoringResult ToScoringResult(TeamResult result) => result switch
    {
        TeamResult.Victory => ScoringResult.Victory,
        TeamResult.Draw => ScoringResult.Draw,
        TeamResult.Defeat => ScoringResult.Defeat,
        _ => throw new DomainRuleException("Undecided result cannot be scored.")
    };

    private static string CreatePveSourceKey(Match match, IReadOnlyCollection<int> aiLevels, IReadOnlyCollection<Guid> humanIds)
    {
        var factionIds = match.Teams.SelectMany(x => x.Participants)
            .Where(x => x.FactionId.HasValue)
            .Select(x => x.FactionId!.Value)
            .Order()
            .ToArray();
        var source = string.Join("|",
            match.GameEditionId,
            match.MapDefinitionId,
            string.Join(',', aiLevels.Order()),
            string.Join(',', humanIds.Order()),
            string.Join(',', factionIds));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant();
    }
}
