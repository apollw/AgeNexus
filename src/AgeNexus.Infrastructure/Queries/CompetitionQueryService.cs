using AgeNexus.Application.Queries;
using AgeNexus.Domain.Competition;
using AgeNexus.Domain.EvidenceAndModeration;
using AgeNexus.Domain.Matches;
using AgeNexus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgeNexus.Infrastructure.Queries;

internal sealed class CompetitionQueryService(AgeNexusDbContext database, CompetitionQueryCache cache) :
    IRankingQueryService,
    IMatchHistoryQueryService,
    IPlayerDirectoryQueryService,
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
            PointScopeKind.Career, seasonId, limit, officialPveOnly: false, cancellationToken),
        RankingBoard.Pve => GetPlayerPointRankingAsync(
            PointScopeKind.Pve, seasonId, limit, officialPveOnly: true, cancellationToken),
        RankingBoard.ClanCompetitive => GetClanRatingRankingAsync(seasonId, limit, cancellationToken),
        RankingBoard.ClanPve => GetClanPointRankingAsync(seasonId, limit, cancellationToken),
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

    private async Task<IReadOnlyCollection<MatchSummary>> GetRecentCoreAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        var recentMatches = database.Matches.AsNoTracking()
            .OrderByDescending(x => x.PlayedAtUtc)
            .Take(limit);

        var rows = await (
            from match in recentMatches
            join creator in database.PlayerProfiles.AsNoTracking()
                on match.CreatedByPlayerProfileId equals creator.Id
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
                match.CreatedByPlayerProfileId,
                creator.ApplicationUserId,
                match.PlayedAtUtc,
                match.Status,
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

        return rows.GroupBy(x => new
            {
                x.MatchId,
                x.CreatedByPlayerProfileId,
                CreatedByApplicationUserId = x.ApplicationUserId,
                x.PlayedAtUtc,
                x.Status
            })
            .OrderByDescending(x => x.Key.PlayedAtUtc)
            .Select(match =>
            {
                var teams = match.GroupBy(x => new { x.TeamId, x.Position, x.Result })
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
                var teamLabels = teams.Select(team =>
                    $"{string.Join(" + ", team.OrderBy(x => x.ParticipantId).Select(participant =>
                        participant.Type == ParticipantType.Human
                            ? participant.ParticipantName ?? "Jogador"
                            : $"IA {participant.ParticipantName ?? "configurada"}"))} ({team.Key.Result})")
                    .ToArray();

                return new MatchSummary(
                    match.Key.MatchId,
                    match.Key.CreatedByPlayerProfileId,
                    match.Key.PlayedAtUtc,
                    category.ToString(),
                    format,
                    match.Key.Status.ToString(),
                    teamLabels)
                {
                    CreatedByApplicationUserId = match.Key.CreatedByApplicationUserId
                };
            })
            .ToArray();
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
                    .Sum(x => (decimal?)x.Points) ?? 0m),
                (database.PointEvents
                    .Where(x => x.BeneficiaryId == player.Id && x.Scope == PointScopeKind.Pve)
                    .Sum(x => (decimal?)x.Points) ?? 0m)))
            .ToListAsync(cancellationToken);
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

        var playerIds = rows.Select(x => x.PlayerProfileId).Distinct().ToArray();
        var names = await database.PlayerProfiles.AsNoTracking()
            .Where(x => playerIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);

        GeneralStatisticBoard Total(
            string key, string category, string title, string description,
            Func<Domain.MatchPerformance.PlayerMatchStatistics, decimal?> selector,
            GeneralStatisticValueKind valueKind = GeneralStatisticValueKind.Integer) =>
            Board(key, category, title, description, valueKind, selector, values => values.Sum(), descending: true);

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
            bool descending)
        {
            var values = rows
                .GroupBy(x => x.PlayerProfileId)
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
                ? values.OrderByDescending(x => aggregate(x.Values)).ThenBy(x => names.GetValueOrDefault(x.PlayerId))
                : values.OrderBy(x => aggregate(x.Values)).ThenBy(x => names.GetValueOrDefault(x.PlayerId));
            var entries = ordered.Take(leadersPerBoard).Select((x, index) => new GeneralStatisticEntry(
                index + 1, x.PlayerId, names.GetValueOrDefault(x.PlayerId, "Jogador"),
                aggregate(x.Values), x.Matches)).ToArray();
            return new GeneralStatisticBoard(key, category, title, description, valueKind, entries);
        }

        var boards = new GeneralStatisticBoard[]
        {
            Total("units-killed", "Combate", "Mais unidades eliminadas", "Total de unidades inimigas eliminadas.", x => x.UnitsKilled),
            Total("units-lost", "Combate", "Mais unidades perdidas", "Total de unidades perdidas durante as batalhas.", x => x.UnitsLost),
            Total("buildings-destroyed", "Combate", "Mais construções destruídas", "Edifícios inimigos destruídos no histórico.", x => x.BuildingsDestroyed),
            Total("buildings-lost", "Combate", "Mais construções perdidas", "Edifícios próprios perdidos no histórico.", x => x.BuildingsLost),
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
        var counts = await database.ClanMemberships.AsNoTracking()
            .Where(x => !x.EndedAtUtc.HasValue)
            .GroupBy(x => x.ClanId)
            .Select(x => new { ClanId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.ClanId, x => x.Count, cancellationToken);
        return clans.Select(x => new ClanSummary(x.Id, x.Name, x.Tag, counts.GetValueOrDefault(x.Id))).ToArray();
    }

    public Task<MatchRegistrationCatalog> GetMatchRegistrationCatalogAsync(
        Guid? gameEditionId = null,
        CancellationToken cancellationToken = default)
        => cache.GetOrCreateAsync(
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
        return new MatchRegistrationCatalog(editions, players, factions, maps, difficulties, seasons, patches);
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
                         x.Scope == PointScopeKind.PerformanceBonus ||
                         (x.Scope == PointScopeKind.Pve &&
                          (x.EvidenceLevel == EvidenceLevel.Verified ||
                           x.EvidenceLevel == EvidenceLevel.Audited))))
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
        var pve = pointRows.Where(x => x.Scope == PointScopeKind.Pve)
            .Select(x => new RankingAggregate(x.BeneficiaryId, x.Score, x.Matches));
        var teamLineups = ratingRows.Where(x => x.Scope == RatingScopeKind.TeamLineup)
            .Select(x => new RankingAggregate(x.BeneficiaryId, x.Score, x.Matches));
        var clanCompetitive = ratingRows.Where(x => x.Scope == RatingScopeKind.ClanCompetitive)
            .Select(x => new RankingAggregate(x.BeneficiaryId, x.Score, x.Matches));

        var generalRows = OrderAndLimit(general, limit);
        var careerRows = OrderAndLimit(career, limit);
        var pveRows = OrderAndLimit(pve, limit);
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
            .Concat(pveRows.Select(x => x.Id))
            .Concat(lineups.Values.SelectMany(x => x.Members).Select(x => x.PlayerProfileId))
            .Distinct()
            .ToArray();
        var playerNames = playerIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await database.PlayerProfiles.AsNoTracking()
                .Where(x => playerIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);

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
                .Select(x => playerNames.GetValueOrDefault(x.PlayerProfileId, "Jogador")))
            : "Equipe";

        return new RankingDashboard(
            ToEntries(generalRows, id => playerNames.GetValueOrDefault(id, "Jogador"), 10),
            ToEntries(careerRows, id => playerNames.GetValueOrDefault(id, "Jogador"), 1),
            ToEntries(pveRows, id => playerNames.GetValueOrDefault(id, "Jogador"), 1),
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
            group ratingEvent by new { ratingEvent.BeneficiaryId, player.DisplayName }
            into events
            select new
            {
                Id = events.Key.BeneficiaryId,
                events.Key.DisplayName,
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
            x.Matches < 10)).ToArray();
    }

    private async Task<IReadOnlyCollection<RankingEntry>> GetPlayerPointRankingAsync(
        PointScopeKind scope,
        Guid? seasonId,
        int limit,
        bool officialPveOnly,
        CancellationToken cancellationToken)
    {
        var query = database.PointEvents.AsNoTracking().Where(x =>
            (x.Scope == scope || (scope == PointScopeKind.Career && x.Scope == PointScopeKind.PerformanceBonus)) &&
            (!seasonId.HasValue || x.SeasonId == seasonId));
        if (officialPveOnly)
        {
            query = query.Where(x => x.EvidenceLevel == EvidenceLevel.Verified || x.EvidenceLevel == EvidenceLevel.Audited);
        }

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

    private sealed record RankingAggregate(Guid Id, decimal Score, int Matches);

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
