using AgeNexus.Application.Queries;
using AgeNexus.Domain.Competition;
using AgeNexus.Domain.EvidenceAndModeration;
using AgeNexus.Domain.MatchPerformance;
using AgeNexus.Domain.Matches;
using AgeNexus.Infrastructure.GameCatalog;
using AgeNexus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgeNexus.Infrastructure.Queries;

internal sealed class CompetitionQueryService(AgeNexusDbContext database, CompetitionQueryCache cache, CatalogQueryCache catalogCache) :
    IRankingQueryService,
    IMatchHistoryQueryService,
    IPlayerDirectoryQueryService,
    IPlayerPublicProfileQueryService,
    IGeneralStatisticsQueryService,
    IStatisticsQueryService,
    IClanQueryService,
    ICatalogQueryService
{
    public Task<IReadOnlyCollection<RankingEntry>> GetAsync(
        RankingBoard board,
        Guid? seasonId = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 500);
        return cache.GetOrCreateAsync(
            $"ranking:{board}:{seasonId}:{limit}",
            token => GetRankingCoreAsync(board, seasonId, limit, token),
            cancellationToken);
    }

    public Task<RankingDashboard> GetDashboardAsync(
        Guid? seasonId = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 500);
        return cache.GetOrCreateAsync(
            $"ranking-dashboard:{seasonId}:{limit}",
            token => GetRankingDashboardCoreAsync(seasonId, limit, token),
            cancellationToken);
    }

    private Task<IReadOnlyCollection<RankingEntry>> GetRankingCoreAsync(
        RankingBoard board,
        Guid? seasonId,
        int limit,
        CancellationToken cancellationToken) => board switch
    {
        RankingBoard.GeneralCompetitive => GetPlayerRatingRankingAsync(
            RatingScopeKind.GeneralCompetitive, seasonId, limit, cancellationToken),
        RankingBoard.TeamLineup => GetTeamLineupRankingAsync(seasonId, limit, cancellationToken),
        RankingBoard.Career => GetPlayerPointRankingAsync(
            PointScopeKind.Career, seasonId, limit, cancellationToken),
        RankingBoard.AiPerformance => GetAiPerformanceRankingAsync(limit, cancellationToken),
        RankingBoard.ClanCompetitive => GetClanRatingRankingAsync(seasonId, limit, cancellationToken),
        _ => throw new ArgumentOutOfRangeException(nameof(board))
    };

    public Task<IReadOnlyCollection<MatchSummary>> GetRecentAsync(
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 200);
        return cache.GetOrCreateAsync(
            $"matches:recent:{limit}",
            token => GetRecentCoreAsync(limit, token),
            cancellationToken);
    }

    public Task<PagedMatchHistory> GetPageAsync(
        MatchHistoryFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var normalized = filter with
        {
            Page = Math.Max(1, filter.Page),
            PageSize = Math.Clamp(filter.PageSize, 5, 50)
        };
        var cacheKey = string.Join(':',
            "matches", "page", normalized.Page, normalized.PageSize,
            normalized.FromDate?.ToString("yyyyMMdd") ?? "-",
            normalized.ToDate?.ToString("yyyyMMdd") ?? "-",
            normalized.PlayerId?.ToString("N") ?? "-",
            normalized.OpponentPlayerId?.ToString("N") ?? "-",
            normalized.MapDefinitionId?.ToString("N") ?? "-",
            normalized.GameMode?.ToString() ?? "-",
            normalized.PendingStatisticsReview);
        return cache.GetOrCreateAsync(
            cacheKey,
            token => GetPageCoreAsync(normalized, token),
            cancellationToken);
    }

    public Task<IReadOnlyCollection<CatalogOption>> GetPlayerOptionsAsync(
        CancellationToken cancellationToken = default) =>
        catalogCache.GetOrCreateAsync<IReadOnlyCollection<CatalogOption>>(
            "match-history:players",
            async token => await database.PlayerProfiles.AsNoTracking()
                .OrderBy(player => player.DisplayName)
                .Select(player => new CatalogOption(player.Id, player.DisplayName))
                .ToArrayAsync(token),
            cancellationToken);

    public Task<IReadOnlyCollection<CatalogOption>> GetMapOptionsAsync(
        CancellationToken cancellationToken = default) =>
        catalogCache.GetOrCreateAsync<IReadOnlyCollection<CatalogOption>>(
            "match-history:maps",
            async token => await database.MapDefinitions.AsNoTracking()
                .OrderBy(map => map.Name)
                .Select(map => new CatalogOption(map.Id, map.Name))
                .ToArrayAsync(token),
            cancellationToken);

    public async Task<MatchMetadataEditView?> GetMetadataForEditAsync(
        Guid matchId,
        CancellationToken cancellationToken = default) =>
        await (
            from match in database.Matches.AsNoTracking()
            join edition in database.GameEditions.AsNoTracking()
                on match.GameEditionId equals edition.Id
            where match.Id == matchId
            select new MatchMetadataEditView(
                match.Id,
                match.GameEditionId,
                edition.Name,
                match.MapDefinitionId,
                match.GameMode))
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<IReadOnlyCollection<MatchSummary>> GetRecentCoreAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        var page = await GetPageCoreAsync(new MatchHistoryFilter(PageSize: limit), cancellationToken);
        return page.Items;
    }

    private async Task<PagedMatchHistory> GetPageCoreAsync(
        MatchHistoryFilter filter,
        CancellationToken cancellationToken)
    {
        var matches = database.Matches.AsNoTracking().AsQueryable();

        if (filter.FromDate.HasValue)
        {
            var from = new DateTimeOffset(filter.FromDate.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            matches = matches.Where(x => x.PlayedAtUtc >= from);
        }

        if (filter.ToDate.HasValue)
        {
            var until = new DateTimeOffset(filter.ToDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            matches = matches.Where(x => x.PlayedAtUtc < until);
        }

        if (filter.PlayerId.HasValue)
        {
            matches = matches.Where(match => match.Teams.Any(team =>
                team.Participants.Any(participant => participant.PlayerProfileId == filter.PlayerId)));
        }

        if (filter.OpponentPlayerId.HasValue)
        {
            if (filter.PlayerId.HasValue)
            {
                matches = matches.Where(match => match.Teams.Any(firstTeam =>
                    firstTeam.Participants.Any(participant => participant.PlayerProfileId == filter.PlayerId) &&
                    match.Teams.Any(secondTeam => secondTeam.Id != firstTeam.Id &&
                        secondTeam.Participants.Any(participant =>
                            participant.PlayerProfileId == filter.OpponentPlayerId))));
            }
            else
            {
                matches = matches.Where(match => match.Teams.Any(team =>
                    team.Participants.Any(participant =>
                        participant.PlayerProfileId == filter.OpponentPlayerId)));
            }
        }

        if (filter.MapDefinitionId.HasValue)
        {
            matches = matches.Where(match => match.MapDefinitionId == filter.MapDefinitionId);
        }

        if (filter.GameMode.HasValue)
        {
            matches = matches.Where(match => match.GameMode == filter.GameMode);
        }

        if (filter.PendingStatisticsReview)
        {
            matches = matches.Where(match => database.MatchStatisticsReports.Any(report =>
                report.MatchId == match.Id && report.Status == MatchStatisticsStatus.Submitted));
        }

        var totalItems = await matches.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)filter.PageSize));
        var pageNumber = Math.Min(filter.Page, totalPages);
        var pageMatches = matches
            .OrderByDescending(x => x.PlayedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip((pageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize);
        var pageHeaders = await (
            from match in pageMatches
            join creator in database.PlayerProfiles.AsNoTracking()
                on match.CreatedByPlayerProfileId equals creator.Id
            join map in database.MapDefinitions.AsNoTracking()
                on match.MapDefinitionId equals (Guid?)map.Id into maps
            from map in maps.DefaultIfEmpty()
            join report in database.MatchStatisticsReports.AsNoTracking()
                on match.Id equals report.MatchId into reports
            from report in reports.DefaultIfEmpty()
            join submitter in database.PlayerProfiles.AsNoTracking()
                on (Guid?)report.SubmittedByPlayerProfileId equals (Guid?)submitter.Id into submitters
            from submitter in submitters.DefaultIfEmpty()
            select new
            {
                MatchId = match.Id,
                match.CreatedByPlayerProfileId,
                CreatedByApplicationUserId = creator.ApplicationUserId,
                match.PlayedAtUtc,
                MatchType = match.Type,
                match.Status,
                match.MapDefinitionId,
                match.GameMode,
                MapName = map == null ? null : map.Name,
                YouTubeVideoUrl = database.MatchEvidence
                    .Where(item => item.MatchId == match.Id && item.Kind == EvidenceKind.VideoLink)
                    .Select(item => item.ExternalUrl)
                    .FirstOrDefault(),
                ScreenshotCount = database.MatchEvidence.Count(item =>
                    item.MatchId == match.Id && item.Kind == EvidenceKind.ResultScreenshot),
                HasReplay = database.MatchEvidence.Any(item =>
                    item.MatchId == match.Id && item.Kind == EvidenceKind.Replay),
                StatisticsStatus = report == null ? (MatchStatisticsStatus?)null : report.Status,
                StatisticsSubmittedByPlayerProfileId = report == null
                    ? (Guid?)null
                    : report.SubmittedByPlayerProfileId,
                StatisticsSubmittedByApplicationUserId = submitter == null
                    ? (Guid?)null
                    : submitter.ApplicationUserId,
                StatisticsSubmittedByDisplayName = submitter == null ? null : submitter.DisplayName
            })
            .ToArrayAsync(cancellationToken);

        if (pageHeaders.Length == 0)
        {
            return new PagedMatchHistory([], pageNumber, filter.PageSize, totalItems);
        }

        var pageMatchIds = pageHeaders.Select(x => x.MatchId).ToArray();
        var participantRows = await (
            from match in database.Matches.AsNoTracking()
            where pageMatchIds.Contains(match.Id)
            from team in match.Teams
            from participant in team.Participants
            join human in database.PlayerProfiles.AsNoTracking()
                on participant.PlayerProfileId equals (Guid?)human.Id into humans
            from human in humans.DefaultIfEmpty()
            join ai in database.AiDifficulties.AsNoTracking()
                on participant.AiDifficultyId equals (Guid?)ai.Id into difficulties
            from ai in difficulties.DefaultIfEmpty()
            select new
            {
                MatchId = match.Id,
                TeamId = team.Id,
                team.Position,
                team.Result,
                ParticipantId = participant.Id,
                participant.Type,
                ParticipantName = participant.Type == ParticipantType.Human
                    ? human.DisplayName
                    : ai.Name
            })
            .ToListAsync(cancellationToken);
        var participantsByMatch = participantRows
            .GroupBy(x => x.MatchId)
            .ToDictionary(x => x.Key, x => x.ToArray());

        var summaries = pageHeaders
            .OrderByDescending(x => x.PlayedAtUtc)
            .ThenByDescending(x => x.MatchId)
            .Select(header =>
            {
                var rows = participantsByMatch.GetValueOrDefault(header.MatchId) ?? [];
                var teams = rows.GroupBy(x => new { x.TeamId, x.Position, x.Result })
                    .OrderBy(x => x.Key.Position)
                    .ToArray();
                var humanCounts = teams
                    .Select(team => team.Count(x => x.Type == ParticipantType.Human))
                    .Where(x => x > 0)
                    .OrderDescending()
                    .ToArray();
                var totalHumans = humanCounts.Sum();
                var totalAi = teams.Sum(team => team.Count(x => x.Type == ParticipantType.ArtificialIntelligence));
                var teamsWithAi = teams.Count(team => team.Any(x => x.Type == ParticipantType.ArtificialIntelligence));
                var category = humanCounts.Length >= 2
                    ? totalAi == 0 ? MatchScoringCategory.PurePvp : MatchScoringCategory.HybridPvp
                    : teams.Length == 2 && humanCounts.Length == 1 && teamsWithAi == 1 &&
                      teams.All(team => team.All(x => x.Type == ParticipantType.Human) ||
                                                team.All(x => x.Type == ParticipantType.ArtificialIntelligence))
                        ? MatchScoringCategory.PurePve
                        : MatchScoringCategory.Ineligible;
                var format = humanCounts.Length >= 2
                    ? string.Join('x', humanCounts)
                    : $"{totalHumans}H x {totalAi}IA";
                var teamSummaries = teams.Select(team => new MatchTeamSummary(
                    team.Key.TeamId,
                    team.Key.Position,
                    team.Key.Result,
                    team.OrderBy(x => x.ParticipantId).Select(participant =>
                        participant.Type == ParticipantType.Human
                            ? participant.ParticipantName ?? "Jogador"
                            : $"IA {participant.ParticipantName ?? "configurada"}").ToArray()))
                    .ToArray();

                return new MatchSummary(
                    header.MatchId,
                    header.CreatedByPlayerProfileId,
                    header.PlayedAtUtc,
                    category,
                    format,
                    header.Status,
                    teamSummaries)
                {
                    Type = header.MatchType,
                    MapDefinitionId = header.MapDefinitionId,
                    MapName = header.MapName,
                    GameMode = header.GameMode,
                    CreatedByApplicationUserId = header.CreatedByApplicationUserId,
                    YouTubeVideoUrl = header.YouTubeVideoUrl,
                    ScreenshotCount = header.ScreenshotCount,
                    HasReplay = header.HasReplay,
                    StatisticsStatus = header.StatisticsStatus,
                    StatisticsSubmittedByPlayerProfileId = header.StatisticsSubmittedByPlayerProfileId,
                    StatisticsSubmittedByApplicationUserId = header.StatisticsSubmittedByApplicationUserId,
                    StatisticsSubmittedByDisplayName = header.StatisticsSubmittedByDisplayName
                };
            })
            .ToArray();

        return new PagedMatchHistory(summaries, pageNumber, filter.PageSize, totalItems);
    }

    public Task<IReadOnlyCollection<PlayerSummary>> GetAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 500);
        return cache.GetOrCreateAsync(
            $"players:{limit}",
            token => GetPlayersCoreAsync(limit, token),
            cancellationToken);
    }

    private async Task<IReadOnlyCollection<PlayerSummary>> GetPlayersCoreAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        return await database.PlayerProfiles.AsNoTracking()
            .OrderBy(x => x.DisplayName)
            .Take(limit)
            .Select(player => new PlayerSummary(
                player.Id,
                player.DisplayName,
                player.AvatarUrl,
                ScoringRuleSet.InitialRating +
                (database.RatingEvents
                    .Where(x => x.Scope == RatingScopeKind.GeneralCompetitive &&
                                x.BeneficiaryId == player.Id)
                    .Sum(x => (decimal?)x.Delta) ?? 0m),
                (database.PointEvents
                    .Where(x => x.BeneficiaryId == player.Id &&
                                (x.Scope == PointScopeKind.Career ||
                                 x.Scope == PointScopeKind.PerformanceBonus))
                    .Sum(x => (decimal?)x.Points) ?? 0m)))
            .ToListAsync(cancellationToken);
    }

    public Task<PlayerPublicProfileDashboard?> GetAsync(
        Guid playerId,
        CancellationToken cancellationToken = default) =>
        GetPlayerProfileCoreAsync(playerId, cancellationToken);

    private async Task<PlayerPublicProfileDashboard?> GetPlayerProfileCoreAsync(
        Guid playerId,
        CancellationToken cancellationToken)
    {
        var profile = await database.PlayerProfiles.AsNoTracking()
            .Where(x => x.Id == playerId)
            .Select(player => new
            {
                player.Id,
                player.DisplayName,
                player.Bio,
                player.Location,
                player.AvatarUrl,
                CompetitiveRating = ScoringRuleSet.InitialRating +
                    (database.RatingEvents.Where(x => x.Scope == RatingScopeKind.GeneralCompetitive &&
                                                       x.BeneficiaryId == player.Id)
                        .Sum(x => (decimal?)x.Delta) ?? 0m),
                CareerPoints = database.PointEvents.Where(x => x.BeneficiaryId == player.Id &&
                    (x.Scope == PointScopeKind.Career || x.Scope == PointScopeKind.PerformanceBonus))
                    .Sum(x => (decimal?)x.Points) ?? 0m
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (profile is null)
        {
            return null;
        }

        var favorite = await (
            from selected in database.PlayerFavoriteFactions.AsNoTracking()
            join faction in database.Factions.AsNoTracking() on selected.FactionId equals faction.Id
            where selected.PlayerProfileId == playerId && selected.Priority == 1
            select new { faction.Name, faction.Slug, faction.ImageUrl })
            .SingleOrDefaultAsync(cancellationToken);

        var matches = await (
            from participant in database.MatchParticipants.AsNoTracking()
            join team in database.MatchTeams.AsNoTracking()
                on EF.Property<Guid>(participant, "team_id") equals team.Id
            join match in database.Matches.AsNoTracking()
                on EF.Property<Guid>(team, "match_id") equals match.Id
            where participant.Type == ParticipantType.Human && participant.PlayerProfileId == playerId &&
                  match.Status == MatchStatus.Validated
            select new { match.Id, team.Result, participant.FactionId })
            .ToArrayAsync(cancellationToken);
        var factionIds = matches.Where(x => x.FactionId.HasValue).Select(x => x.FactionId!.Value)
            .Distinct().ToArray();
        var factions = await database.Factions.AsNoTracking().Where(x => factionIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var factionSummaries = matches.Where(x => x.FactionId.HasValue)
            .GroupBy(x => x.FactionId!.Value)
            .Select(group =>
            {
                var uses = group.Count();
                var victories = group.Count(x => x.Result == TeamResult.Victory);
                var draws = group.Count(x => x.Result == TeamResult.Draw);
                var faction = factions[group.Key];
                return new PlayerPublicFactionSummary(
                    faction.Id, faction.Name, faction.ImageUrl, uses, victories,
                    Math.Round((victories + draws * 0.5m) / uses * 100m, 2));
            })
            .OrderByDescending(x => x.Uses).ThenByDescending(x => x.WinRate).ThenBy(x => x.Name)
            .ToArray();
        var victories = matches.Count(x => x.Result == TeamResult.Victory);
        var draws = matches.Count(x => x.Result == TeamResult.Draw);
        var defeats = matches.Count(x => x.Result == TeamResult.Defeat);
        var winRate = matches.Length == 0 ? 0m : Math.Round((victories + draws * 0.5m) / matches.Length * 100m, 2);

        return new PlayerPublicProfileDashboard(
            profile.Id, profile.DisplayName, profile.Bio, profile.Location, profile.AvatarUrl,
            favorite?.Name,
            favorite is null
                ? null
                : favorite.ImageUrl ?? AgeNexus.Infrastructure.GameCatalog.Age2DefinitiveEditionCatalog.GetCivilizationImagePath(favorite.Slug),
            profile.CompetitiveRating, profile.CareerPoints,
            matches.Length, victories, draws, defeats, winRate, factionSummaries);
    }

    async Task<GeneralStatisticsDashboard> IGeneralStatisticsQueryService.GetAsync(
        int leadersPerBoard,
        CancellationToken cancellationToken)
    {
        leadersPerBoard = Math.Clamp(leadersPerBoard, 1, 20);
        var rows = await (
            from statistic in database.PlayerMatchStatistics.AsNoTracking()
            join report in database.MatchStatisticsReports.AsNoTracking()
                on statistic.ReportId equals report.Id
            join match in database.Matches.AsNoTracking()
                on statistic.MatchId equals match.Id
            where match.Status == MatchStatus.Validated &&
                  (report.Status == Domain.MatchPerformance.MatchStatisticsStatus.Confirmed ||
                   report.Status == Domain.MatchPerformance.MatchStatisticsStatus.Awarded)
            select statistic).ToArrayAsync(cancellationToken);

        rows = rows.Where(x => x.PlayerProfileId.HasValue).ToArray();
        var playerIds = rows.Select(x => x.PlayerProfileId!.Value).Distinct().ToArray();
        var playersById = await database.PlayerProfiles.AsNoTracking()
            .Where(x => playerIds.Contains(x.Id))
            .Select(x => new PlayerIdentity(x.Id, x.DisplayName, x.AvatarUrl))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        GeneralStatisticBoard Total(
            string key, string category, string title, string description,
            Func<Domain.MatchPerformance.PlayerMatchStatistics, decimal?> selector,
            GeneralStatisticValueKind valueKind = GeneralStatisticValueKind.Integer,
            bool isNegative = false) =>
            Board(key, category, title, description, valueKind, selector, values => values.Sum(), descending: true, isNegative: isNegative);

        GeneralStatisticBoard Average(
            string key, string category, string title, string description,
            Func<Domain.MatchPerformance.PlayerMatchStatistics, decimal?> selector,
            GeneralStatisticValueKind valueKind = GeneralStatisticValueKind.Decimal) =>
            Board(key, category, title, description, valueKind, selector, values => values.Average(), descending: true);

        GeneralStatisticBoard Record(
            string key, string category, string title, string description,
            Func<Domain.MatchPerformance.PlayerMatchStatistics, decimal?> selector,
            GeneralStatisticValueKind valueKind = GeneralStatisticValueKind.Integer,
            bool lowerIsBetter = false) =>
            Board(key, category, title, description, valueKind, selector,
                values => lowerIsBetter ? values.Min() : values.Max(), descending: !lowerIsBetter);

        GeneralStatisticBoard Board(
            string key, string category, string title, string description,
            GeneralStatisticValueKind valueKind,
            Func<Domain.MatchPerformance.PlayerMatchStatistics, decimal?> selector,
            Func<IReadOnlyCollection<decimal>, decimal> aggregate,
            bool descending,
            bool isNegative = false)
        {
            var values = rows
                .GroupBy(x => x.PlayerProfileId!.Value)
                .Select(group =>
                {
                    var available = group.Select(selector).Where(x => x.HasValue)
                        .Select(x => x!.Value).ToArray();
                    return new
                    {
                        PlayerId = group.Key,
                        Values = available,
                        Matches = group.Where(x => selector(x).HasValue).Select(x => x.MatchId).Distinct().Count()
                    };
                })
                .Where(x => x.Values.Length > 0);
            var ordered = descending
                ? values.OrderByDescending(x => aggregate(x.Values)).ThenBy(x => playersById.GetValueOrDefault(x.PlayerId)?.DisplayName)
                : values.OrderBy(x => aggregate(x.Values)).ThenBy(x => playersById.GetValueOrDefault(x.PlayerId)?.DisplayName);
            var entries = ordered.Take(leadersPerBoard).Select((x, index) => new GeneralStatisticEntry(
                index + 1, x.PlayerId,
                playersById.GetValueOrDefault(x.PlayerId)?.DisplayName ?? "Jogador",
                playersById.GetValueOrDefault(x.PlayerId)?.AvatarUrl,
                aggregate(x.Values), x.Matches)).ToArray();
            return new GeneralStatisticBoard(key, category, title, description, valueKind, entries, isNegative);
        }

        var boards = new GeneralStatisticBoard[]
        {
            Total("units-killed", "Combate", "Mais unidades eliminadas", "Total de unidades inimigas eliminadas.", x => x.UnitsKilled),
            Total("units-lost", "Combate", "Mais unidades perdidas", "Total de unidades perdidas durante as batalhas.", x => x.UnitsLost, isNegative: true),
            Total("buildings-destroyed", "Combate", "Mais construções destruídas", "Edifícios inimigos destruídos no histórico.", x => x.BuildingsDestroyed),
            Total("buildings-lost", "Combate", "Mais construções perdidas", "Edifícios próprios perdidos no histórico.", x => x.BuildingsLost, isNegative: true),
            Total("conversions", "Combate", "Mais conversões", "Unidades convertidas por monges.", x => x.UnitsConverted),
            Record("largest-army", "Combate", "Maior exército", "Maior exército registrado em uma única partida.", x => x.LargestArmy),

            Total("food", "Economia", "Maior coleta de comida", "Soma de toda a comida coletada.", x => x.FoodCollected),
            Total("wood", "Economia", "Maior coleta de madeira", "Soma de toda a madeira coletada.", x => x.WoodCollected),
            Total("gold", "Economia", "Maior coleta de ouro", "Soma de todo o ouro coletado.", x => x.GoldCollected),
            Total("stone", "Economia", "Maior coleta de pedra", "Soma de toda a pedra coletada.", x => x.StoneCollected),
            Total("trade-gold", "Economia", "Mais ouro comercial", "Ouro produzido por comércio.", x => x.TradeGold),
            Total("relic-gold", "Economia", "Mais ouro de relíquias", "Ouro gerado por relíquias.", x => x.RelicGold),

            Total("research", "Tecnologia", "Mais tecnologias pesquisadas", "Total de tecnologias concluídas.", x => x.ResearchCount),
            Average("explored", "Tecnologia", "Maior exploração média", "Percentual médio do mapa explorado.", x => x.ExploredPercent, GeneralStatisticValueKind.Percentage),
            Record("fastest-feudal", "Tecnologia", "Feudal mais rápido", "Menor tempo registrado para chegar à Era Feudal.", x => x.FeudalAgeSeconds, GeneralStatisticValueKind.Duration, true),
            Record("fastest-castle", "Tecnologia", "Castelos mais rápido", "Menor tempo registrado para chegar à Era dos Castelos.", x => x.CastleAgeSeconds, GeneralStatisticValueKind.Duration, true),
            Record("fastest-imperial", "Tecnologia", "Imperial mais rápido", "Menor tempo registrado para chegar à Era Imperial.", x => x.ImperialAgeSeconds, GeneralStatisticValueKind.Duration, true),

            Record("villagers", "Sociedade", "Maior população de aldeões", "Maior pico de aldeões em uma partida.", x => x.PeakVillagers),
            Total("castles", "Sociedade", "Mais castelos construídos", "Total de castelos construídos.", x => x.CastlesBuilt),
            Total("wonders", "Sociedade", "Mais maravilhas construídas", "Total de maravilhas construídas.", x => x.WondersBuilt),
            Total("relics", "Sociedade", "Mais relíquias capturadas", "Total de relíquias capturadas.", x => x.RelicsCaptured),
            Average("total-score", "Placar", "Maior pontuação média", "Média da pontuação total nas partidas.", x => x.TotalScore),
            Average("military-score", "Placar", "Maior placar militar médio", "Média da pontuação militar.", x => x.MilitaryScore),
            Average("economy-score", "Placar", "Maior placar econômico médio", "Média da pontuação econômica.", x => x.EconomyScore)
        };

        return new GeneralStatisticsDashboard(
            rows.Select(x => x.MatchId).Distinct().Count(),
            playerIds.Length,
            rows.Length,
            boards);
    }

    public Task<IReadOnlyCollection<FactionStatistics>> GetFactionStatisticsAsync(
        Guid? gameEditionId = null,
        CancellationToken cancellationToken = default)
        => cache.GetOrCreateAsync(
            $"factions:{gameEditionId}",
            token => GetFactionStatisticsCoreAsync(gameEditionId, token),
            cancellationToken);

    private async Task<IReadOnlyCollection<FactionStatistics>> GetFactionStatisticsCoreAsync(
        Guid? gameEditionId,
        CancellationToken cancellationToken)
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
                var draws = group.Count(x => x.Result == TeamResult.Draw);
                var faction = factions[group.Key];
                return new FactionStatistics(
                    group.Key,
                    faction.Name,
                    faction.ImageUrl ?? Age2DefinitiveEditionCatalog.GetCivilizationImagePath(faction.Slug),
                    uses,
                    victories,
                    draws,
                    group.Count(x => x.Result == TeamResult.Defeat),
                    uses == 0 ? 0m : Math.Round((decimal)victories / uses * 100m, 2),
                    FactionRankingCalculator.CalculateStrengthIndex(uses, victories, draws));
            })
            .OrderByDescending(x => x.StrengthIndex)
            .ThenByDescending(x => x.Uses)
            .ThenByDescending(x => x.WinRate)
            .ThenBy(x => x.Name)
            .ToArray();
    }

    public Task<IReadOnlyCollection<PlayerFactionStatistics>> GetPlayerFactionStatisticsAsync(
        Guid playerId,
        CancellationToken cancellationToken = default)
        => cache.GetOrCreateAsync(
            $"player-factions:{playerId}",
            token => GetPlayerFactionStatisticsCoreAsync(playerId, token),
            cancellationToken);

    private async Task<IReadOnlyCollection<PlayerFactionStatistics>> GetPlayerFactionStatisticsCoreAsync(
        Guid playerId,
        CancellationToken cancellationToken)
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

    Task<IReadOnlyCollection<ClanSummary>> IClanQueryService.GetAsync(CancellationToken cancellationToken)
        => cache.GetOrCreateAsync(
            "clans",
            GetClansCoreAsync,
            cancellationToken);

    private async Task<IReadOnlyCollection<ClanSummary>> GetClansCoreAsync(CancellationToken cancellationToken)
    {
        var clans = await database.Clans.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var members = await (
            from membership in database.ClanMemberships.AsNoTracking()
            join player in database.PlayerProfiles.AsNoTracking()
                on membership.PlayerProfileId equals player.Id
            where !membership.EndedAtUtc.HasValue
            orderby player.DisplayName
            select new
            {
                membership.ClanId,
                Member = new ClanMemberSummary(player.Id, player.DisplayName, player.AvatarUrl)
            })
            .ToArrayAsync(cancellationToken);
        var membersByClan = members
            .GroupBy(x => x.ClanId)
            .ToDictionary(x => x.Key, x => (IReadOnlyCollection<ClanMemberSummary>)x.Select(y => y.Member).ToArray());
        return clans.Select(x => new ClanSummary(
            x.Id,
            x.Name,
            x.Tag,
            membersByClan.GetValueOrDefault(x.Id) ?? [])).ToArray();
    }

    public Task<MatchRegistrationCatalog> GetMatchRegistrationCatalogAsync(
        Guid? gameEditionId = null,
        CancellationToken cancellationToken = default)
        => catalogCache.GetOrCreateAsync(
            $"catalog:match:{gameEditionId}",
            token => GetMatchRegistrationCatalogCoreAsync(gameEditionId, token),
            cancellationToken);

    private async Task<MatchRegistrationCatalog> GetMatchRegistrationCatalogCoreAsync(
        Guid? gameEditionId,
        CancellationToken cancellationToken)
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
        var clans = await GetClansCoreAsync(cancellationToken);
        return new MatchRegistrationCatalog(editions, players, factions, maps, difficulties, seasons, patches, clans);
    }

    private async Task<RankingDashboard> GetRankingDashboardCoreAsync(
        Guid? seasonId,
        int limit,
        CancellationToken cancellationToken)
    {
        var ratingScopes = new[]
        {
            RatingScopeKind.GeneralCompetitive,
            RatingScopeKind.TeamLineup,
            RatingScopeKind.ClanCompetitive
        };
        var ratingRows = await database.RatingEvents.AsNoTracking()
            .Where(x => ratingScopes.Contains(x.Scope) && (!seasonId.HasValue || x.SeasonId == seasonId))
            .GroupBy(x => new { x.Scope, x.BeneficiaryId })
            .Select(group => new RatingRankingAggregate
            {
                Scope = group.Key.Scope,
                BeneficiaryId = group.Key.BeneficiaryId,
                Score = ScoringRuleSet.InitialRating + group.Sum(x => x.Delta),
                Matches = group.Sum(x => x.Kind == ScoringEventKind.Award ? 1 : -1)
            })
            .ToListAsync(cancellationToken);

        var pointRows = await database.PointEvents.AsNoTracking()
            .Where(x => (!seasonId.HasValue || x.SeasonId == seasonId) &&
                        (x.Scope == PointScopeKind.Career ||
                         x.Scope == PointScopeKind.PerformanceBonus))
            .GroupBy(x => new { x.Scope, x.BeneficiaryId })
            .Select(group => new PointRankingAggregate
            {
                Scope = group.Key.Scope,
                BeneficiaryId = group.Key.BeneficiaryId,
                Score = group.Sum(x => x.Points),
                Matches = group.Sum(x => x.Kind == ScoringEventKind.Award ? 1 : -1)
            })
            .ToListAsync(cancellationToken);

        var general = ratingRows.Where(x => x.Scope == RatingScopeKind.GeneralCompetitive)
            .Select(x => new RankingAggregate(x.BeneficiaryId, x.Score, x.Matches));
        var career = pointRows
            .Where(x => x.Scope is PointScopeKind.Career or PointScopeKind.PerformanceBonus)
            .GroupBy(x => x.BeneficiaryId)
            .Select(group => new RankingAggregate(
                group.Key,
                group.Sum(x => x.Score),
                group.Where(x => x.Scope == PointScopeKind.Career).Sum(x => x.Matches)));
        var teamLineups = ratingRows.Where(x => x.Scope == RatingScopeKind.TeamLineup)
            .Select(x => new RankingAggregate(x.BeneficiaryId, x.Score, x.Matches));
        var clanCompetitive = ratingRows.Where(x => x.Scope == RatingScopeKind.ClanCompetitive)
            .Select(x => new RankingAggregate(x.BeneficiaryId, x.Score, x.Matches));

        var generalRows = OrderAndLimit(general, limit);
        var careerRows = OrderAndLimit(career, limit);
        var aiRows = await GetAiPerformanceRankingAsync(limit, cancellationToken);
        var teamRows = OrderAndLimit(teamLineups, limit);
        var clanRows = OrderAndLimit(clanCompetitive, limit);

        var lineupIds = teamRows.Select(x => x.Id).ToArray();
        var lineups = lineupIds.Length == 0
            ? new Dictionary<Guid, TeamLineup>()
            : await database.TeamLineups.AsNoTracking()
                .Where(x => lineupIds.Contains(x.Id))
                .Include(x => x.Members)
                .ToDictionaryAsync(x => x.Id, cancellationToken);

        var playerIds = generalRows.Select(x => x.Id)
            .Concat(careerRows.Select(x => x.Id))
            .Concat(lineups.Values.SelectMany(x => x.Members).Select(x => x.PlayerProfileId))
            .Distinct()
            .ToArray();
        var players = playerIds.Length == 0
            ? new Dictionary<Guid, PlayerIdentity>()
            : await database.PlayerProfiles.AsNoTracking()
                .Where(x => playerIds.Contains(x.Id))
                .Select(x => new PlayerIdentity(x.Id, x.DisplayName, x.AvatarUrl))
                .ToDictionaryAsync(x => x.Id, cancellationToken);

        var clanIds = clanRows.Select(x => x.Id).ToArray();
        var clanDetails = clanIds.Length == 0
            ? Array.Empty<ClanName>()
            : await database.Clans.AsNoTracking()
                .Where(x => clanIds.Contains(x.Id))
                .Select(x => new ClanName(x.Id, x.Tag, x.Name))
                .ToArrayAsync(cancellationToken);
        var clanNames = clanDetails.ToDictionary(x => x.Id, x => $"[{x.Tag}] {x.Name}");

        string LineupName(Guid id) => lineups.TryGetValue(id, out var lineup)
            ? string.Join(" + ", lineup.Members.OrderBy(x => x.Position)
                .Select(x => players.GetValueOrDefault(x.PlayerProfileId)?.DisplayName ?? "Jogador"))
            : "Equipe";

        return new RankingDashboard(
            ToPlayerEntries(generalRows, players, 10),
            ToPlayerEntries(careerRows, players, 1),
            aiRows,
            ToEntries(teamRows, LineupName, 5),
            ToEntries(clanRows, id => clanNames.GetValueOrDefault(id, "Clã"), 5));
    }

    private static RankingAggregate[] OrderAndLimit(IEnumerable<RankingAggregate> rows, int limit) => rows
        .Where(x => x.Matches > 0)
        .OrderByDescending(x => x.Score)
        .ThenBy(x => x.Id)
        .Take(limit)
        .ToArray();

    private static IReadOnlyCollection<RankingEntry> ToEntries(
        IReadOnlyCollection<RankingAggregate> rows,
        Func<Guid, string> displayName,
        int provisionalThreshold) => rows.Select((x, index) => new RankingEntry(
            index + 1,
            x.Id,
            displayName(x.Id),
            x.Score,
            x.Matches,
            x.Matches < provisionalThreshold)).ToArray();

    private static IReadOnlyCollection<RankingEntry> ToPlayerEntries(
        IReadOnlyCollection<RankingAggregate> rows,
        IReadOnlyDictionary<Guid, PlayerIdentity> players,
        int provisionalThreshold) => rows.Select((x, index) => new RankingEntry(
            index + 1,
            x.Id,
            players.GetValueOrDefault(x.Id)?.DisplayName ?? "Jogador",
            x.Score,
            x.Matches,
            x.Matches < provisionalThreshold)
        {
            AvatarUrl = players.GetValueOrDefault(x.Id)?.AvatarUrl
        }).ToArray();

    private async Task<IReadOnlyCollection<RankingEntry>> GetPlayerRatingRankingAsync(
        RatingScopeKind scope,
        Guid? seasonId,
        int limit,
        CancellationToken cancellationToken)
    {
        var aggregates = await (
            from ratingEvent in database.RatingEvents.AsNoTracking()
            join player in database.PlayerProfiles.AsNoTracking()
                on ratingEvent.BeneficiaryId equals player.Id
            where ratingEvent.Scope == scope &&
                  (!seasonId.HasValue || ratingEvent.SeasonId == seasonId)
            group ratingEvent by new { ratingEvent.BeneficiaryId, player.DisplayName, player.AvatarUrl }
            into events
            select new
            {
                Id = events.Key.BeneficiaryId,
                events.Key.DisplayName,
                events.Key.AvatarUrl,
                Score = ScoringRuleSet.InitialRating + events.Sum(e => e.Delta),
                Matches = events.Sum(e => e.Kind == ScoringEventKind.Award ? 1 : -1)
            })
            .Where(x => x.Matches > 0)
            .OrderByDescending(x => x.Score)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(cancellationToken);
        return aggregates.Select((x, index) => new RankingEntry(
            index + 1,
            x.Id,
            x.DisplayName,
            x.Score,
            x.Matches,
            x.Matches < 10)
        {
            AvatarUrl = x.AvatarUrl
        }).ToArray();
    }

    private async Task<IReadOnlyCollection<RankingEntry>> GetPlayerPointRankingAsync(
        PointScopeKind scope,
        Guid? seasonId,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = database.PointEvents.AsNoTracking().Where(x =>
            (x.Scope == scope || (scope == PointScopeKind.Career && x.Scope == PointScopeKind.PerformanceBonus)) &&
            (!seasonId.HasValue || x.SeasonId == seasonId));

        var aggregates = await query.GroupBy(x => x.BeneficiaryId)
            .Select(x => new
            {
                Id = x.Key,
                Score = x.Sum(e => e.Points),
                Matches = x.Sum(e => e.Scope == scope
                    ? (e.Kind == ScoringEventKind.Award ? 1 : -1)
                    : 0)
            })
            .Where(x => x.Matches > 0)
            .OrderByDescending(x => x.Score)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(cancellationToken);
        return await AttachPlayerNamesAsync(aggregates.Select(x => (x.Id, x.Score, x.Matches)).ToArray(), 1, cancellationToken);
    }

    private async Task<IReadOnlyCollection<RankingEntry>> GetAiPerformanceRankingAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        var samples = await (
            from statistic in database.PlayerMatchStatistics.AsNoTracking()
            join report in database.MatchStatisticsReports.AsNoTracking() on statistic.ReportId equals report.Id
            join match in database.Matches.AsNoTracking() on statistic.MatchId equals match.Id
            join difficulty in database.AiDifficulties.AsNoTracking() on statistic.AiDifficultyId equals (Guid?)difficulty.Id
            where statistic.AiDifficultyId.HasValue && statistic.TotalScore.HasValue &&
                  match.Type == Domain.Matches.MatchType.Mixed && match.Status == MatchStatus.Validated &&
                  (report.Status == Domain.MatchPerformance.MatchStatisticsStatus.Confirmed ||
                   report.Status == Domain.MatchPerformance.MatchStatisticsStatus.Awarded)
            select new
            {
                DifficultyId = difficulty.Id,
                difficulty.Name,
                TotalScore = statistic.TotalScore!.Value,
                statistic.IsTeamMvp
            }).ToArrayAsync(cancellationToken);

        return samples
            .GroupBy(x => new { x.DifficultyId, x.Name })
            .Select(group => new
            {
                group.Key.DifficultyId,
                group.Key.Name,
                AverageScore = Math.Round(group.Average(x => (decimal)x.TotalScore), 2),
                Samples = group.Count(),
                MvpAwards = group.Count(x => x.IsTeamMvp)
            })
            .OrderByDescending(x => x.AverageScore)
            .ThenByDescending(x => x.MvpAwards)
            .ThenBy(x => x.Name)
            .Take(Math.Clamp(limit, 1, 500))
            .Select((x, index) => new RankingEntry(
                index + 1,
                x.DifficultyId,
                $"IA {x.Name}",
                x.AverageScore,
                x.Samples,
                x.Samples < 5)
            {
                MvpAwards = x.MvpAwards
            })
            .ToArray();
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
        var players = await database.PlayerProfiles.AsNoTracking().Where(x => ids.Contains(x.Id))
            .Select(x => new PlayerIdentity(x.Id, x.DisplayName, x.AvatarUrl))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        return aggregates.Select((x, index) => new RankingEntry(
            index + 1,
            x.Id,
            players.GetValueOrDefault(x.Id)?.DisplayName ?? "Jogador",
            x.Score,
            x.Matches,
            x.Matches < provisionalThreshold)
        {
            AvatarUrl = players.GetValueOrDefault(x.Id)?.AvatarUrl
        }).ToArray();
    }

    private sealed record RankingAggregate(Guid Id, decimal Score, int Matches);

    private sealed record PlayerIdentity(Guid Id, string DisplayName, string? AvatarUrl);

    private sealed record ClanName(Guid Id, string Tag, string Name);

    private sealed class RatingRankingAggregate
    {
        public RatingScopeKind Scope { get; init; }
        public Guid BeneficiaryId { get; init; }
        public decimal Score { get; init; }
        public int Matches { get; init; }
    }

    private sealed class PointRankingAggregate
    {
        public PointScopeKind Scope { get; init; }
        public Guid BeneficiaryId { get; init; }
        public decimal Score { get; init; }
        public int Matches { get; init; }
    }
}
