using AgeNexus.Application.Queries;
using AgeNexus.Domain.Competition;
using AgeNexus.Domain.EvidenceAndModeration;
using AgeNexus.Domain.Matches;
using AgeNexus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgeNexus.Infrastructure.Queries;

public sealed class CompetitionQueryService(AgeNexusDbContext database) :
    IRankingQueryService,
    IMatchHistoryQueryService,
    IPlayerDirectoryQueryService,
    IStatisticsQueryService,
    IClanQueryService,
    ICatalogQueryService
{
    public Task<IReadOnlyCollection<RankingEntry>> GetAsync(
        RankingBoard board,
        Guid? seasonId = null,
        int limit = 100,
        CancellationToken cancellationToken = default) => board switch
    {
        RankingBoard.GeneralCompetitive => GetPlayerRatingRankingAsync(
            RatingScopeKind.GeneralCompetitive, seasonId, limit, cancellationToken),
        RankingBoard.TeamLineup => GetTeamLineupRankingAsync(seasonId, limit, cancellationToken),
        RankingBoard.Career => GetPlayerPointRankingAsync(
            PointScopeKind.Career, seasonId, limit, officialPveOnly: false, cancellationToken),
        RankingBoard.Pve => GetPlayerPointRankingAsync(
            PointScopeKind.Pve, seasonId, limit, officialPveOnly: true, cancellationToken),
        RankingBoard.ClanCompetitive => GetClanRatingRankingAsync(seasonId, limit, cancellationToken),
        RankingBoard.ClanPve => GetClanPointRankingAsync(seasonId, limit, cancellationToken),
        _ => throw new ArgumentOutOfRangeException(nameof(board))
    };

    public async Task<IReadOnlyCollection<MatchSummary>> GetRecentAsync(
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 200);
        var matches = await database.Matches.AsNoTracking()
            .Include(x => x.Teams)
            .ThenInclude(x => x.Participants)
            .OrderByDescending(x => x.PlayedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
        var humanIds = matches.SelectMany(x => x.Teams).SelectMany(x => x.Participants)
            .Where(x => x.PlayerProfileId.HasValue)
            .Select(x => x.PlayerProfileId!.Value)
            .Distinct()
            .ToArray();
        var names = await database.PlayerProfiles.AsNoTracking()
            .Where(x => humanIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);
        var aiIds = matches.SelectMany(x => x.Teams).SelectMany(x => x.Participants)
            .Where(x => x.AiDifficultyId.HasValue)
            .Select(x => x.AiDifficultyId!.Value)
            .Distinct()
            .ToArray();
        var aiNames = await database.AiDifficulties.AsNoTracking()
            .Where(x => aiIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        return matches.Select(match => new MatchSummary(
            match.Id,
            match.PlayedAtUtc,
            match.ScoringCategory.ToString(),
            match.HumanFormatLabel ?? $"{match.Teams.Sum(x => x.HumanCount)}H x {match.Teams.Sum(x => x.AiCount)}IA",
            match.Status.ToString(),
            match.Teams.OrderBy(x => x.Position).Select(team =>
                $"{string.Join(" + ", team.Participants.Select(participant => participant.Type == ParticipantType.Human
                    ? names.GetValueOrDefault(participant.PlayerProfileId!.Value, "Jogador")
                    : $"IA {aiNames.GetValueOrDefault(participant.AiDifficultyId!.Value, "configurada")}"))} ({team.Result})")
                .ToArray())).ToArray();
    }

    public async Task<IReadOnlyCollection<PlayerSummary>> GetAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 500);
        var players = await database.PlayerProfiles.AsNoTracking()
            .OrderBy(x => x.DisplayName)
            .Take(limit)
            .ToListAsync(cancellationToken);
        var ids = players.Select(x => x.Id).ToArray();
        var ratings = await database.RatingEvents.AsNoTracking()
            .Where(x => x.Scope == RatingScopeKind.GeneralCompetitive && ids.Contains(x.BeneficiaryId))
            .GroupBy(x => x.BeneficiaryId)
            .Select(x => new { Id = x.Key, Value = x.Sum(e => e.Delta) })
            .ToDictionaryAsync(x => x.Id, x => x.Value, cancellationToken);
        var points = await database.PointEvents.AsNoTracking()
            .Where(x => ids.Contains(x.BeneficiaryId) &&
                        (x.Scope == PointScopeKind.Career || x.Scope == PointScopeKind.Pve))
            .GroupBy(x => new { x.BeneficiaryId, x.Scope })
            .Select(x => new { x.Key.BeneficiaryId, x.Key.Scope, Value = x.Sum(e => e.Points) })
            .ToListAsync(cancellationToken);
        var pointLookup = points.ToDictionary(x => (x.BeneficiaryId, x.Scope), x => x.Value);
        return players.Select(x => new PlayerSummary(
            x.Id,
            x.DisplayName,
            x.AvatarUrl,
            ScoringRuleSet.InitialRating + ratings.GetValueOrDefault(x.Id),
            pointLookup.GetValueOrDefault((x.Id, PointScopeKind.Career)),
            pointLookup.GetValueOrDefault((x.Id, PointScopeKind.Pve)))).ToArray();
    }

    public async Task<IReadOnlyCollection<FactionStatistics>> GetFactionStatisticsAsync(
        Guid? gameEditionId = null,
        CancellationToken cancellationToken = default)
    {
        var rows = await (
            from participant in database.MatchParticipants.AsNoTracking()
            join team in database.MatchTeams.AsNoTracking()
                on EF.Property<Guid>(participant, "team_id") equals team.Id
            join match in database.Matches.AsNoTracking()
                on EF.Property<Guid>(team, "match_id") equals match.Id
            where match.Status == MatchStatus.Validated && participant.FactionId.HasValue &&
                  (!gameEditionId.HasValue || match.GameEditionId == gameEditionId.Value)
            select new { FactionId = participant.FactionId!.Value, team.Result })
            .ToListAsync(cancellationToken);
        var factionIds = rows.Select(x => x.FactionId).Distinct().ToArray();
        var factions = await database.Factions.AsNoTracking()
            .Where(x => factionIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        return rows.GroupBy(x => x.FactionId)
            .Select(group =>
            {
                var uses = group.Count();
                var victories = group.Count(x => x.Result == TeamResult.Victory);
                var faction = factions[group.Key];
                return new FactionStatistics(
                    group.Key,
                    faction.Name,
                    faction.ImageUrl,
                    uses,
                    victories,
                    group.Count(x => x.Result == TeamResult.Draw),
                    group.Count(x => x.Result == TeamResult.Defeat),
                    uses == 0 ? 0m : Math.Round((decimal)victories / uses * 100m, 2));
            })
            .OrderByDescending(x => x.Uses)
            .ThenByDescending(x => x.WinRate)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<PlayerFactionStatistics>> GetPlayerFactionStatisticsAsync(
        Guid playerId,
        CancellationToken cancellationToken = default)
    {
        var rows = await (
            from participant in database.MatchParticipants.AsNoTracking()
            join team in database.MatchTeams.AsNoTracking()
                on EF.Property<Guid>(participant, "team_id") equals team.Id
            join match in database.Matches.AsNoTracking()
                on EF.Property<Guid>(team, "match_id") equals match.Id
            where match.Status == MatchStatus.Validated && participant.PlayerProfileId == playerId &&
                  participant.FactionId.HasValue
            select new { FactionId = participant.FactionId!.Value, team.Result })
            .ToListAsync(cancellationToken);
        var factionIds = rows.Select(x => x.FactionId).Distinct().ToArray();
        var names = await database.Factions.AsNoTracking()
            .Where(x => factionIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        return rows.GroupBy(x => x.FactionId).Select(group =>
        {
            var uses = group.Count();
            var victories = group.Count(x => x.Result == TeamResult.Victory);
            return new PlayerFactionStatistics(
                playerId,
                group.Key,
                names[group.Key],
                uses,
                victories,
                uses == 0 ? 0m : Math.Round((decimal)victories / uses * 100m, 2));
        }).OrderByDescending(x => x.Uses).ToArray();
    }

    async Task<IReadOnlyCollection<ClanSummary>> IClanQueryService.GetAsync(CancellationToken cancellationToken)
    {
        var clans = await database.Clans.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var counts = await database.ClanMemberships.AsNoTracking()
            .Where(x => !x.EndedAtUtc.HasValue)
            .GroupBy(x => x.ClanId)
            .Select(x => new { ClanId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.ClanId, x => x.Count, cancellationToken);
        return clans.Select(x => new ClanSummary(x.Id, x.Name, x.Tag, counts.GetValueOrDefault(x.Id))).ToArray();
    }

    public async Task<MatchRegistrationCatalog> GetMatchRegistrationCatalogAsync(
        Guid? gameEditionId = null,
        CancellationToken cancellationToken = default)
    {
        var editions = await database.GameEditions.AsNoTracking().Where(x => x.IsActive)
            .OrderBy(x => x.Name).Select(x => new CatalogOption(x.Id, x.Name)).ToArrayAsync(cancellationToken);
        var selectedEditionId = gameEditionId ?? editions.FirstOrDefault()?.Id;
        var players = await database.PlayerProfiles.AsNoTracking().OrderBy(x => x.DisplayName)
            .Select(x => new CatalogOption(x.Id, x.DisplayName)).ToArrayAsync(cancellationToken);
        var factions = await database.Factions.AsNoTracking()
            .Where(x => !selectedEditionId.HasValue || x.GameEditionId == selectedEditionId)
            .OrderBy(x => x.Name).Select(x => new CatalogOption(x.Id, x.Name)).ToArrayAsync(cancellationToken);
        var maps = await database.MapDefinitions.AsNoTracking()
            .Where(x => !selectedEditionId.HasValue || x.GameEditionId == selectedEditionId)
            .OrderBy(x => x.Name).Select(x => new CatalogOption(x.Id, x.Name)).ToArrayAsync(cancellationToken);
        var difficulties = await database.AiDifficulties.AsNoTracking()
            .Where(x => !selectedEditionId.HasValue || x.GameEditionId == selectedEditionId)
            .OrderBy(x => x.InternalLevel).Select(x => new CatalogOption(x.Id, x.Name)).ToArrayAsync(cancellationToken);
        var seasons = await database.Seasons.AsNoTracking()
            .Where(x => !selectedEditionId.HasValue || x.GameEditionId == selectedEditionId)
            .OrderByDescending(x => x.StartsAtUtc).Select(x => new CatalogOption(x.Id, x.Name)).ToArrayAsync(cancellationToken);
        var patches = await database.GamePatches.AsNoTracking()
            .Where(x => !selectedEditionId.HasValue || x.GameEditionId == selectedEditionId)
            .OrderByDescending(x => x.EffectiveFromUtc).Select(x => new CatalogOption(x.Id, x.Name)).ToArrayAsync(cancellationToken);
        return new MatchRegistrationCatalog(editions, players, factions, maps, difficulties, seasons, patches);
    }

    private async Task<IReadOnlyCollection<RankingEntry>> GetPlayerRatingRankingAsync(
        RatingScopeKind scope,
        Guid? seasonId,
        int limit,
        CancellationToken cancellationToken)
    {
        var aggregates = await database.RatingEvents.AsNoTracking()
            .Where(x => x.Scope == scope && (!seasonId.HasValue || x.SeasonId == seasonId))
            .GroupBy(x => x.BeneficiaryId)
            .Select(x => new
            {
                Id = x.Key,
                Score = ScoringRuleSet.InitialRating + x.Sum(e => e.Delta),
                Matches = x.Sum(e => e.Kind == ScoringEventKind.Award ? 1 : -1)
            })
            .Where(x => x.Matches > 0)
            .OrderByDescending(x => x.Score)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(cancellationToken);
        return await AttachPlayerNamesAsync(aggregates.Select(x => (x.Id, x.Score, x.Matches)).ToArray(), 10, cancellationToken);
    }

    private async Task<IReadOnlyCollection<RankingEntry>> GetPlayerPointRankingAsync(
        PointScopeKind scope,
        Guid? seasonId,
        int limit,
        bool officialPveOnly,
        CancellationToken cancellationToken)
    {
        var query = database.PointEvents.AsNoTracking().Where(x =>
            x.Scope == scope && (!seasonId.HasValue || x.SeasonId == seasonId));
        if (officialPveOnly)
        {
            query = query.Where(x => x.EvidenceLevel == EvidenceLevel.Verified || x.EvidenceLevel == EvidenceLevel.Audited);
        }

        var aggregates = await query.GroupBy(x => x.BeneficiaryId)
            .Select(x => new
            {
                Id = x.Key,
                Score = x.Sum(e => e.Points),
                Matches = x.Sum(e => e.Kind == ScoringEventKind.Award ? 1 : -1)
            })
            .Where(x => x.Matches > 0)
            .OrderByDescending(x => x.Score)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(cancellationToken);
        return await AttachPlayerNamesAsync(aggregates.Select(x => (x.Id, x.Score, x.Matches)).ToArray(), 1, cancellationToken);
    }

    private async Task<IReadOnlyCollection<RankingEntry>> GetTeamLineupRankingAsync(
        Guid? seasonId,
        int limit,
        CancellationToken cancellationToken)
    {
        var aggregates = await database.RatingEvents.AsNoTracking()
            .Where(x => x.Scope == RatingScopeKind.TeamLineup && (!seasonId.HasValue || x.SeasonId == seasonId))
            .GroupBy(x => x.BeneficiaryId)
            .Select(x => new
            {
                Id = x.Key,
                Score = ScoringRuleSet.InitialRating + x.Sum(e => e.Delta),
                Matches = x.Sum(e => e.Kind == ScoringEventKind.Award ? 1 : -1)
            })
            .Where(x => x.Matches > 0)
            .OrderByDescending(x => x.Score)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(cancellationToken);
        var ids = aggregates.Select(x => x.Id).ToArray();
        var lineups = await database.TeamLineups.AsNoTracking().Where(x => ids.Contains(x.Id))
            .Include(x => x.Members).ToDictionaryAsync(x => x.Id, cancellationToken);
        var playerIds = lineups.Values.SelectMany(x => x.Members).Select(x => x.PlayerProfileId).Distinct().ToArray();
        var names = await database.PlayerProfiles.AsNoTracking().Where(x => playerIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);
        return aggregates.Select((x, index) => new RankingEntry(
            index + 1,
            x.Id,
            string.Join(" + ", lineups[x.Id].Members.OrderBy(m => m.Position).Select(m => names[m.PlayerProfileId])),
            x.Score,
            x.Matches,
            x.Matches < 5)).ToArray();
    }

    private Task<IReadOnlyCollection<RankingEntry>> GetClanRatingRankingAsync(
        Guid? seasonId,
        int limit,
        CancellationToken cancellationToken) =>
        GetClanRankingCoreAsync(RatingScopeKind.ClanCompetitive, null, seasonId, limit, cancellationToken);

    private Task<IReadOnlyCollection<RankingEntry>> GetClanPointRankingAsync(
        Guid? seasonId,
        int limit,
        CancellationToken cancellationToken) =>
        GetClanRankingCoreAsync(null, PointScopeKind.ClanPve, seasonId, limit, cancellationToken);

    private async Task<IReadOnlyCollection<RankingEntry>> GetClanRankingCoreAsync(
        RatingScopeKind? ratingScope,
        PointScopeKind? pointScope,
        Guid? seasonId,
        int limit,
        CancellationToken cancellationToken)
    {
        List<(Guid Id, decimal Score, int Matches)> aggregates;
        if (ratingScope.HasValue)
        {
            aggregates = await database.RatingEvents.AsNoTracking()
                .Where(x => x.Scope == ratingScope.Value && (!seasonId.HasValue || x.SeasonId == seasonId))
                .GroupBy(x => x.BeneficiaryId)
                .Select(x => new ValueTuple<Guid, decimal, int>(
                    x.Key,
                    ScoringRuleSet.InitialRating + x.Sum(e => e.Delta),
                    x.Sum(e => e.Kind == ScoringEventKind.Award ? 1 : -1)))
                .ToListAsync(cancellationToken);
        }
        else
        {
            aggregates = await database.PointEvents.AsNoTracking()
                .Where(x => x.Scope == pointScope!.Value && (!seasonId.HasValue || x.SeasonId == seasonId))
                .GroupBy(x => x.BeneficiaryId)
                .Select(x => new ValueTuple<Guid, decimal, int>(
                    x.Key,
                    x.Sum(e => e.Points),
                    x.Sum(e => e.Kind == ScoringEventKind.Award ? 1 : -1)))
                .ToListAsync(cancellationToken);
        }

        aggregates = aggregates.Where(x => x.Matches > 0).OrderByDescending(x => x.Score)
            .Take(Math.Clamp(limit, 1, 500)).ToList();
        var ids = aggregates.Select(x => x.Id).ToArray();
        var clans = await database.Clans.AsNoTracking().Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        return aggregates.Select((x, index) => new RankingEntry(
            index + 1, x.Id, $"[{clans[x.Id].Tag}] {clans[x.Id].Name}", x.Score, x.Matches, x.Matches < 5)).ToArray();
    }

    private async Task<IReadOnlyCollection<RankingEntry>> AttachPlayerNamesAsync(
        IReadOnlyCollection<(Guid Id, decimal Score, int Matches)> aggregates,
        int provisionalThreshold,
        CancellationToken cancellationToken)
    {
        var ids = aggregates.Select(x => x.Id).ToArray();
        var names = await database.PlayerProfiles.AsNoTracking().Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);
        return aggregates.Select((x, index) => new RankingEntry(
            index + 1,
            x.Id,
            names.GetValueOrDefault(x.Id, "Jogador"),
            x.Score,
            x.Matches,
            x.Matches < provisionalThreshold)).ToArray();
    }
}
