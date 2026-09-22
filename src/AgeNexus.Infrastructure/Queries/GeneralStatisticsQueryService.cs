using AgeNexus.Application.Queries;
using AgeNexus.Domain.MatchPerformance;
using AgeNexus.Domain.Matches;
using AgeNexus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgeNexus.Infrastructure.Queries;

internal sealed class GeneralStatisticsQueryService(
    AgeNexusDbContext database,
    CompetitionQueryCache cache) : IGeneralStatisticsQueryService
{
    public Task<GeneralStatisticsDashboard> GetAsync(
        int leadersPerBoard = 5,
        CancellationToken cancellationToken = default)
    {
        leadersPerBoard = Math.Clamp(leadersPerBoard, 1, 20);
        return cache.GetOrCreateAsync(
            $"statistics:general:{leadersPerBoard}",
            token => GetCoreAsync(leadersPerBoard, token),
            cancellationToken);
    }

    private async Task<GeneralStatisticsDashboard> GetCoreAsync(
        int leadersPerBoard,
        CancellationToken cancellationToken)
    {
        var source =
            from statistic in database.PlayerMatchStatistics.AsNoTracking()
            join report in database.MatchStatisticsReports.AsNoTracking()
                on statistic.ReportId equals report.Id
            join match in database.Matches.AsNoTracking()
                on statistic.MatchId equals match.Id
            join team in database.MatchTeams.AsNoTracking()
                on statistic.TeamId equals team.Id
            join player in database.PlayerProfiles.AsNoTracking()
                on statistic.PlayerProfileId equals (Guid?)player.Id
            where statistic.PlayerProfileId.HasValue && match.Status == MatchStatus.Validated &&
                  (report.Status == MatchStatisticsStatus.Confirmed ||
                   report.Status == MatchStatisticsStatus.Awarded)
            select new { Statistic = statistic, team.Result, PlayerId = player.Id, player.DisplayName, player.AvatarUrl };

        var rows = await source
            .GroupBy(x => new { x.PlayerId, x.DisplayName, x.AvatarUrl })
            .Select(group => new GeneralStatisticsAggregate
            {
                PlayerId = group.Key.PlayerId,
                DisplayName = group.Key.DisplayName,
                AvatarUrl = group.Key.AvatarUrl,
                RowCount = group.Count(),
                Defeats = group.Count(x => x.Result == TeamResult.Defeat),
                UnitsKilled = group.Sum(x => (decimal?)x.Statistic.UnitsKilled),
                UnitsKilledMatches = group.Count(x => x.Statistic.UnitsKilled.HasValue),
                UnitsLost = group.Sum(x => (decimal?)x.Statistic.UnitsLost),
                UnitsLostMatches = group.Count(x => x.Statistic.UnitsLost.HasValue),
                BuildingsDestroyed = group.Sum(x => (decimal?)x.Statistic.BuildingsDestroyed),
                BuildingsDestroyedMatches = group.Count(x => x.Statistic.BuildingsDestroyed.HasValue),
                BuildingsLost = group.Sum(x => (decimal?)x.Statistic.BuildingsLost),
                BuildingsLostMatches = group.Count(x => x.Statistic.BuildingsLost.HasValue),
                UnitsConverted = group.Sum(x => (decimal?)x.Statistic.UnitsConverted),
                UnitsConvertedMatches = group.Count(x => x.Statistic.UnitsConverted.HasValue),
                LargestArmy = group.Max(x => (decimal?)x.Statistic.LargestArmy),
                LargestArmyTotal = group.Sum(x => (decimal?)x.Statistic.LargestArmy),
                LargestArmyMatches = group.Count(x => x.Statistic.LargestArmy.HasValue),
                FoodCollected = group.Sum(x => (decimal?)x.Statistic.FoodCollected),
                FoodCollectedMatches = group.Count(x => x.Statistic.FoodCollected.HasValue),
                WoodCollected = group.Sum(x => (decimal?)x.Statistic.WoodCollected),
                WoodCollectedMatches = group.Count(x => x.Statistic.WoodCollected.HasValue),
                GoldCollected = group.Sum(x => (decimal?)x.Statistic.GoldCollected),
                GoldCollectedMatches = group.Count(x => x.Statistic.GoldCollected.HasValue),
                StoneCollected = group.Sum(x => (decimal?)x.Statistic.StoneCollected),
                StoneCollectedMatches = group.Count(x => x.Statistic.StoneCollected.HasValue),
                TradeGold = group.Sum(x => (decimal?)x.Statistic.TradeGold),
                TradeGoldMatches = group.Count(x => x.Statistic.TradeGold.HasValue),
                RelicGold = group.Sum(x => (decimal?)x.Statistic.RelicGold),
                RelicGoldMatches = group.Count(x => x.Statistic.RelicGold.HasValue),
                ResearchCount = group.Sum(x => (decimal?)x.Statistic.ResearchCount),
                ResearchCountMatches = group.Count(x => x.Statistic.ResearchCount.HasValue),
                ExploredPercent = group.Average(x => (decimal?)x.Statistic.ExploredPercent),
                ExploredPercentTotal = group.Sum(x => (decimal?)x.Statistic.ExploredPercent),
                ExploredPercentMatches = group.Count(x => x.Statistic.ExploredPercent.HasValue),
                FeudalAgeSeconds = group.Min(x => (decimal?)x.Statistic.FeudalAgeSeconds),
                FeudalAgeSecondsTotal = group.Sum(x => (decimal?)x.Statistic.FeudalAgeSeconds),
                FeudalAgeMatches = group.Count(x => x.Statistic.FeudalAgeSeconds.HasValue),
                CastleAgeSeconds = group.Min(x => (decimal?)x.Statistic.CastleAgeSeconds),
                CastleAgeSecondsTotal = group.Sum(x => (decimal?)x.Statistic.CastleAgeSeconds),
                CastleAgeMatches = group.Count(x => x.Statistic.CastleAgeSeconds.HasValue),
                ImperialAgeSeconds = group.Min(x => (decimal?)x.Statistic.ImperialAgeSeconds),
                ImperialAgeSecondsTotal = group.Sum(x => (decimal?)x.Statistic.ImperialAgeSeconds),
                ImperialAgeMatches = group.Count(x => x.Statistic.ImperialAgeSeconds.HasValue),
                PeakVillagers = group.Max(x => (decimal?)x.Statistic.PeakVillagers),
                PeakVillagersTotal = group.Sum(x => (decimal?)x.Statistic.PeakVillagers),
                PeakVillagersMatches = group.Count(x => x.Statistic.PeakVillagers.HasValue),
                CastlesBuilt = group.Sum(x => (decimal?)x.Statistic.CastlesBuilt),
                CastlesBuiltMatches = group.Count(x => x.Statistic.CastlesBuilt.HasValue),
                WondersBuilt = group.Sum(x => (decimal?)x.Statistic.WondersBuilt),
                WondersBuiltMatches = group.Count(x => x.Statistic.WondersBuilt.HasValue),
                RelicsCaptured = group.Sum(x => (decimal?)x.Statistic.RelicsCaptured),
                RelicsCapturedMatches = group.Count(x => x.Statistic.RelicsCaptured.HasValue),
                TotalScore = group.Average(x => (decimal?)x.Statistic.TotalScore),
                TotalScoreTotal = group.Sum(x => (decimal?)x.Statistic.TotalScore),
                TotalScoreMatches = group.Count(x => x.Statistic.TotalScore.HasValue),
                MilitaryScore = group.Average(x => (decimal?)x.Statistic.MilitaryScore),
                MilitaryScoreTotal = group.Sum(x => (decimal?)x.Statistic.MilitaryScore),
                MilitaryScoreMatches = group.Count(x => x.Statistic.MilitaryScore.HasValue),
                EconomyScore = group.Average(x => (decimal?)x.Statistic.EconomyScore),
                EconomyScoreTotal = group.Sum(x => (decimal?)x.Statistic.EconomyScore),
                EconomyScoreMatches = group.Count(x => x.Statistic.EconomyScore.HasValue)
            })
            .ToArrayAsync(cancellationToken);

        var matchesWithStatistics = await source.Select(x => x.Statistic.MatchId)
            .Distinct()
            .CountAsync(cancellationToken);

        GeneralStatisticBoard Board(
            string key,
            string category,
            string title,
            string description,
            GeneralStatisticValueKind valueKind,
            Func<GeneralStatisticsAggregate, decimal?> value,
            Func<GeneralStatisticsAggregate, int> matches,
            Func<GeneralStatisticsAggregate, decimal?>? absolute = null,
            Func<GeneralStatisticsAggregate, decimal?>? average = null,
            bool descending = true,
            bool isNegative = false,
            GeneralStatisticValueKind? averageValueKind = null,
            string absoluteLabel = "Total",
            string averageLabel = "Média por partida")
        {
            var available = rows.Where(x => matches(x) > 0 && value(x).HasValue);
            var ordered = descending
                ? available.OrderByDescending(x => value(x)!.Value).ThenBy(x => x.DisplayName)
                : available.OrderBy(x => value(x)!.Value).ThenBy(x => x.DisplayName);
            var entries = ordered.Take(leadersPerBoard)
                .Select((x, index) => new GeneralStatisticEntry(
                    index + 1, x.PlayerId, x.DisplayName, x.AvatarUrl, value(x)!.Value, matches(x),
                    absolute?.Invoke(x) ?? value(x),
                    average?.Invoke(x) ?? Average(absolute?.Invoke(x) ?? value(x), matches(x))))
                .ToArray();
            return new GeneralStatisticBoard(key, category, title, description, valueKind, entries,
                isNegative, averageValueKind, absoluteLabel, averageLabel);
        }

        GeneralStatisticBoard LowestAverage(
            string key, string category, string title, string description,
            GeneralStatisticValueKind valueKind,
            Func<GeneralStatisticsAggregate, decimal?> total,
            Func<GeneralStatisticsAggregate, int> matches,
            string absoluteLabel = "Total") =>
            Board(key, category, title, description, valueKind,
                x => Average(total(x), matches(x)), matches, total,
                x => Average(total(x), matches(x)), descending: false, isNegative: true,
                absoluteLabel: absoluteLabel);

        static decimal? Average(decimal? total, int matches) =>
            total.HasValue && matches > 0 ? total.Value / matches : null;

        var boards = new[]
        {
            Board("units-killed", "Combate", "O Ceifador", "Maior total de unidades inimigas eliminadas.", GeneralStatisticValueKind.Integer, x => x.UnitsKilled, x => x.UnitsKilledMatches),
            Board("units-lost", "Combate", "O Doador de Experiência", "Maior total de unidades próprias perdidas em batalha.", GeneralStatisticValueKind.Integer, x => x.UnitsLost, x => x.UnitsLostMatches, isNegative: true),
            Board("buildings-destroyed", "Combate", "O Demolidor", "Maior total de edifícios inimigos destruídos.", GeneralStatisticValueKind.Integer, x => x.BuildingsDestroyed, x => x.BuildingsDestroyedMatches),
            Board("buildings-lost", "Combate", "O Sem-Teto", "Maior total de edifícios próprios perdidos.", GeneralStatisticValueKind.Integer, x => x.BuildingsLost, x => x.BuildingsLostMatches, isNegative: true),
            Board("conversions", "Combate", "O Inquisidor", "Maior total de unidades convertidas por monges.", GeneralStatisticValueKind.Integer, x => x.UnitsConverted, x => x.UnitsConvertedMatches),
            Board("largest-army", "Combate", "O Senhor das Hostes", "Maior exército alcançado em uma partida; a média mostra o tamanho habitual dos exércitos.", GeneralStatisticValueKind.Integer, x => x.LargestArmy, x => x.LargestArmyMatches, x => x.LargestArmy, x => Average(x.LargestArmyTotal, x.LargestArmyMatches), absoluteLabel: "Maior marca"),
            Board("food", "Economia", "O Mestre das Provisões", "Maior soma de comida coletada.", GeneralStatisticValueKind.Integer, x => x.FoodCollected, x => x.FoodCollectedMatches),
            Board("wood", "Economia", "O Lenhador-Mor", "Maior soma de madeira coletada.", GeneralStatisticValueKind.Integer, x => x.WoodCollected, x => x.WoodCollectedMatches),
            Board("gold", "Economia", "O Toque de Midas", "Maior soma de ouro coletado.", GeneralStatisticValueKind.Integer, x => x.GoldCollected, x => x.GoldCollectedMatches),
            Board("stone", "Economia", "O Mestre da Pedreira", "Maior soma de pedra coletada.", GeneralStatisticValueKind.Integer, x => x.StoneCollected, x => x.StoneCollectedMatches),
            Board("trade-gold", "Economia", "O Magnata das Rotas", "Maior total de ouro produzido por comércio.", GeneralStatisticValueKind.Integer, x => x.TradeGold, x => x.TradeGoldMatches),
            Board("relic-gold", "Economia", "O Cofre Sagrado", "Maior total de ouro gerado por relíquias.", GeneralStatisticValueKind.Integer, x => x.RelicGold, x => x.RelicGoldMatches),
            Board("research", "Tecnologia", "O Sábio", "Maior total de tecnologias concluídas.", GeneralStatisticValueKind.Integer, x => x.ResearchCount, x => x.ResearchCountMatches),
            Board("explored", "Tecnologia", "O Cartógrafo", "Maior exploração média do mapa ao longo das partidas.", GeneralStatisticValueKind.Percentage, x => x.ExploredPercent, x => x.ExploredPercentMatches, x => x.ExploredPercentTotal, x => x.ExploredPercent),
            Board("fastest-feudal", "Tecnologia", "O Precursor", "Menor tempo registrado para chegar à Era Feudal; a média revela a velocidade habitual.", GeneralStatisticValueKind.Duration, x => x.FeudalAgeSeconds, x => x.FeudalAgeMatches, x => x.FeudalAgeSeconds, x => Average(x.FeudalAgeSecondsTotal, x.FeudalAgeMatches), descending: false, absoluteLabel: "Melhor tempo"),
            Board("fastest-castle", "Tecnologia", "O Senhor Feudal", "Menor tempo registrado para chegar à Era dos Castelos; a média revela a velocidade habitual.", GeneralStatisticValueKind.Duration, x => x.CastleAgeSeconds, x => x.CastleAgeMatches, x => x.CastleAgeSeconds, x => Average(x.CastleAgeSecondsTotal, x.CastleAgeMatches), descending: false, absoluteLabel: "Melhor tempo"),
            Board("fastest-imperial", "Tecnologia", "O Imperador Relâmpago", "Menor tempo registrado para chegar à Era Imperial; a média revela a velocidade habitual.", GeneralStatisticValueKind.Duration, x => x.ImperialAgeSeconds, x => x.ImperialAgeMatches, x => x.ImperialAgeSeconds, x => Average(x.ImperialAgeSecondsTotal, x.ImperialAgeMatches), descending: false, absoluteLabel: "Melhor tempo"),
            Board("villagers", "Sociedade", "O Pai da Nação", "Maior pico de aldeões em uma partida; a média mostra a população econômica habitual.", GeneralStatisticValueKind.Integer, x => x.PeakVillagers, x => x.PeakVillagersMatches, x => x.PeakVillagers, x => Average(x.PeakVillagersTotal, x.PeakVillagersMatches), absoluteLabel: "Maior marca"),
            Board("castles", "Sociedade", "O Rei dos Castelos", "Maior total de castelos construídos.", GeneralStatisticValueKind.Integer, x => x.CastlesBuilt, x => x.CastlesBuiltMatches),
            Board("wonders", "Sociedade", "O Arquiteto do Impossível", "Maior total de maravilhas construídas.", GeneralStatisticValueKind.Integer, x => x.WondersBuilt, x => x.WondersBuiltMatches),
            Board("relics", "Sociedade", "O Caçador de Relíquias", "Maior total de relíquias capturadas.", GeneralStatisticValueKind.Integer, x => x.RelicsCaptured, x => x.RelicsCapturedMatches),
            Board("total-score", "Placar", "A Lenda do Nexus", "Maior pontuação total média nas partidas.", GeneralStatisticValueKind.Decimal, x => x.TotalScore, x => x.TotalScoreMatches, x => x.TotalScoreTotal, x => x.TotalScore),
            Board("military-score", "Placar", "O Estrategista", "Maior pontuação militar média nas partidas.", GeneralStatisticValueKind.Decimal, x => x.MilitaryScore, x => x.MilitaryScoreMatches, x => x.MilitaryScoreTotal, x => x.MilitaryScore),
            Board("economy-score", "Placar", "O Grão-Mestre da Economia", "Maior pontuação econômica média nas partidas.", GeneralStatisticValueKind.Decimal, x => x.EconomyScore, x => x.EconomyScoreMatches, x => x.EconomyScoreTotal, x => x.EconomyScore),

            Board("defeats", "Resultados", "O Veterano das Derrotas", "Maior número de derrotas em partidas validadas; a média é exibida como taxa de derrotas.", GeneralStatisticValueKind.Integer, x => x.Defeats, x => x.RowCount, x => x.Defeats, x => x.RowCount > 0 ? x.Defeats * 100m / x.RowCount : null, isNegative: true, averageValueKind: GeneralStatisticValueKind.Percentage, absoluteLabel: "Derrotas", averageLabel: "Taxa de derrotas"),
            LowestAverage("least-kills", "Combate", "O Exército Inofensivo", "Menor média de unidades inimigas eliminadas por partida.", GeneralStatisticValueKind.Integer, x => x.UnitsKilled, x => x.UnitsKilledMatches),
            LowestAverage("least-destruction", "Combate", "O Cerco Sem Impacto", "Menor média de edifícios inimigos destruídos por partida.", GeneralStatisticValueKind.Integer, x => x.BuildingsDestroyed, x => x.BuildingsDestroyedMatches),
            LowestAverage("least-conversions", "Combate", "O Monge Distraído", "Menor média de unidades convertidas por partida.", GeneralStatisticValueKind.Integer, x => x.UnitsConverted, x => x.UnitsConvertedMatches),
            LowestAverage("smallest-army", "Combate", "A Menor Hoste", "Menor tamanho médio de exército registrado.", GeneralStatisticValueKind.Integer, x => x.LargestArmyTotal, x => x.LargestArmyMatches),
            LowestAverage("least-food", "Economia", "O Celeiro Vazio", "Menor média de comida coletada por partida.", GeneralStatisticValueKind.Integer, x => x.FoodCollected, x => x.FoodCollectedMatches),
            LowestAverage("least-wood", "Economia", "A Serraria Parada", "Menor média de madeira coletada por partida.", GeneralStatisticValueKind.Integer, x => x.WoodCollected, x => x.WoodCollectedMatches),
            LowestAverage("least-gold", "Economia", "O Cofre Vazio", "Menor média de ouro coletado por partida.", GeneralStatisticValueKind.Integer, x => x.GoldCollected, x => x.GoldCollectedMatches),
            LowestAverage("least-stone", "Economia", "A Pedreira Abandonada", "Menor média de pedra coletada por partida.", GeneralStatisticValueKind.Integer, x => x.StoneCollected, x => x.StoneCollectedMatches),
            LowestAverage("worst-market", "Economia", "O Pior Mercado", "Menor média de ouro produzido pelo comércio por partida.", GeneralStatisticValueKind.Integer, x => x.TradeGold, x => x.TradeGoldMatches),
            LowestAverage("least-relic-gold", "Economia", "O Cofre Profanado", "Menor média de ouro gerado por relíquias por partida.", GeneralStatisticValueKind.Integer, x => x.RelicGold, x => x.RelicGoldMatches),
            LowestAverage("least-research", "Tecnologia", "O Atrasado", "Menor média de tecnologias concluídas por partida.", GeneralStatisticValueKind.Integer, x => x.ResearchCount, x => x.ResearchCountMatches),
            LowestAverage("least-explored", "Tecnologia", "O Mapa em Branco", "Menor percentual médio de mapa explorado.", GeneralStatisticValueKind.Percentage, x => x.ExploredPercentTotal, x => x.ExploredPercentMatches),
            Board("slowest-feudal", "Tecnologia", "O Feudal Tardio", "Maior tempo médio para chegar à Era Feudal.", GeneralStatisticValueKind.Duration, x => Average(x.FeudalAgeSecondsTotal, x.FeudalAgeMatches), x => x.FeudalAgeMatches, x => x.FeudalAgeSeconds, x => Average(x.FeudalAgeSecondsTotal, x.FeudalAgeMatches), isNegative: true, absoluteLabel: "Melhor tempo"),
            Board("slowest-castle", "Tecnologia", "Os Castelos Distantes", "Maior tempo médio para chegar à Era dos Castelos.", GeneralStatisticValueKind.Duration, x => Average(x.CastleAgeSecondsTotal, x.CastleAgeMatches), x => x.CastleAgeMatches, x => x.CastleAgeSeconds, x => Average(x.CastleAgeSecondsTotal, x.CastleAgeMatches), isNegative: true, absoluteLabel: "Melhor tempo"),
            Board("slowest-imperial", "Tecnologia", "O Império Atrasado", "Maior tempo médio para chegar à Era Imperial.", GeneralStatisticValueKind.Duration, x => Average(x.ImperialAgeSecondsTotal, x.ImperialAgeMatches), x => x.ImperialAgeMatches, x => x.ImperialAgeSeconds, x => Average(x.ImperialAgeSecondsTotal, x.ImperialAgeMatches), isNegative: true, absoluteLabel: "Melhor tempo"),
            LowestAverage("least-villagers", "Sociedade", "A Vila Pequena", "Menor média de pico de aldeões por partida.", GeneralStatisticValueKind.Integer, x => x.PeakVillagersTotal, x => x.PeakVillagersMatches),
            LowestAverage("least-castles", "Sociedade", "O Reino Sem Castelos", "Menor média de castelos construídos por partida.", GeneralStatisticValueKind.Integer, x => x.CastlesBuilt, x => x.CastlesBuiltMatches),
            LowestAverage("least-wonders", "Sociedade", "O Sonho Inacabado", "Menor média de maravilhas construídas por partida.", GeneralStatisticValueKind.Integer, x => x.WondersBuilt, x => x.WondersBuiltMatches),
            LowestAverage("least-relics", "Sociedade", "O Relicário Vazio", "Menor média de relíquias capturadas por partida.", GeneralStatisticValueKind.Integer, x => x.RelicsCaptured, x => x.RelicsCapturedMatches),
            LowestAverage("worst-total-score", "Placar", "A Menor Pontuação", "Menor pontuação total média nas partidas.", GeneralStatisticValueKind.Decimal, x => x.TotalScoreTotal, x => x.TotalScoreMatches),
            LowestAverage("worst-military-score", "Placar", "O Pior Placar Militar", "Menor pontuação militar média nas partidas.", GeneralStatisticValueKind.Decimal, x => x.MilitaryScoreTotal, x => x.MilitaryScoreMatches),
            LowestAverage("worst-economy", "Placar", "A Pior Economia", "Menor pontuação econômica média nas partidas.", GeneralStatisticValueKind.Decimal, x => x.EconomyScoreTotal, x => x.EconomyScoreMatches)
        };

        return new GeneralStatisticsDashboard(
            matchesWithStatistics,
            rows.Length,
            rows.Sum(x => x.RowCount),
            boards);
    }

    private sealed class GeneralStatisticsAggregate
    {
        public Guid PlayerId { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public string? AvatarUrl { get; init; }
        public int RowCount { get; init; }
        public int Defeats { get; init; }
        public decimal? UnitsKilled { get; init; }
        public int UnitsKilledMatches { get; init; }
        public decimal? UnitsLost { get; init; }
        public int UnitsLostMatches { get; init; }
        public decimal? BuildingsDestroyed { get; init; }
        public int BuildingsDestroyedMatches { get; init; }
        public decimal? BuildingsLost { get; init; }
        public int BuildingsLostMatches { get; init; }
        public decimal? UnitsConverted { get; init; }
        public int UnitsConvertedMatches { get; init; }
        public decimal? LargestArmy { get; init; }
        public decimal? LargestArmyTotal { get; init; }
        public int LargestArmyMatches { get; init; }
        public decimal? FoodCollected { get; init; }
        public int FoodCollectedMatches { get; init; }
        public decimal? WoodCollected { get; init; }
        public int WoodCollectedMatches { get; init; }
        public decimal? GoldCollected { get; init; }
        public int GoldCollectedMatches { get; init; }
        public decimal? StoneCollected { get; init; }
        public int StoneCollectedMatches { get; init; }
        public decimal? TradeGold { get; init; }
        public int TradeGoldMatches { get; init; }
        public decimal? RelicGold { get; init; }
        public int RelicGoldMatches { get; init; }
        public decimal? ResearchCount { get; init; }
        public int ResearchCountMatches { get; init; }
        public decimal? ExploredPercent { get; init; }
        public decimal? ExploredPercentTotal { get; init; }
        public int ExploredPercentMatches { get; init; }
        public decimal? FeudalAgeSeconds { get; init; }
        public decimal? FeudalAgeSecondsTotal { get; init; }
        public int FeudalAgeMatches { get; init; }
        public decimal? CastleAgeSeconds { get; init; }
        public decimal? CastleAgeSecondsTotal { get; init; }
        public int CastleAgeMatches { get; init; }
        public decimal? ImperialAgeSeconds { get; init; }
        public decimal? ImperialAgeSecondsTotal { get; init; }
        public int ImperialAgeMatches { get; init; }
        public decimal? PeakVillagers { get; init; }
        public decimal? PeakVillagersTotal { get; init; }
        public int PeakVillagersMatches { get; init; }
        public decimal? CastlesBuilt { get; init; }
        public int CastlesBuiltMatches { get; init; }
        public decimal? WondersBuilt { get; init; }
        public int WondersBuiltMatches { get; init; }
        public decimal? RelicsCaptured { get; init; }
        public int RelicsCapturedMatches { get; init; }
        public decimal? TotalScore { get; init; }
        public decimal? TotalScoreTotal { get; init; }
        public int TotalScoreMatches { get; init; }
        public decimal? MilitaryScore { get; init; }
        public decimal? MilitaryScoreTotal { get; init; }
        public int MilitaryScoreMatches { get; init; }
        public decimal? EconomyScore { get; init; }
        public decimal? EconomyScoreTotal { get; init; }
        public int EconomyScoreMatches { get; init; }
    }
}
