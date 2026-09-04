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
            join player in database.PlayerProfiles.AsNoTracking()
                on statistic.PlayerProfileId equals player.Id
            where match.Status == MatchStatus.Validated &&
                  (report.Status == MatchStatisticsStatus.Confirmed ||
                   report.Status == MatchStatisticsStatus.Awarded)
            select new { Statistic = statistic, player.DisplayName };

        var rows = await source
            .GroupBy(x => new { x.Statistic.PlayerProfileId, x.DisplayName })
            .Select(group => new GeneralStatisticsAggregate
            {
                PlayerId = group.Key.PlayerProfileId,
                DisplayName = group.Key.DisplayName,
                RowCount = group.Count(),
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
                ExploredPercentMatches = group.Count(x => x.Statistic.ExploredPercent.HasValue),
                FeudalAgeSeconds = group.Min(x => (decimal?)x.Statistic.FeudalAgeSeconds),
                FeudalAgeMatches = group.Count(x => x.Statistic.FeudalAgeSeconds.HasValue),
                CastleAgeSeconds = group.Min(x => (decimal?)x.Statistic.CastleAgeSeconds),
                CastleAgeMatches = group.Count(x => x.Statistic.CastleAgeSeconds.HasValue),
                ImperialAgeSeconds = group.Min(x => (decimal?)x.Statistic.ImperialAgeSeconds),
                ImperialAgeMatches = group.Count(x => x.Statistic.ImperialAgeSeconds.HasValue),
                PeakVillagers = group.Max(x => (decimal?)x.Statistic.PeakVillagers),
                PeakVillagersMatches = group.Count(x => x.Statistic.PeakVillagers.HasValue),
                CastlesBuilt = group.Sum(x => (decimal?)x.Statistic.CastlesBuilt),
                CastlesBuiltMatches = group.Count(x => x.Statistic.CastlesBuilt.HasValue),
                WondersBuilt = group.Sum(x => (decimal?)x.Statistic.WondersBuilt),
                WondersBuiltMatches = group.Count(x => x.Statistic.WondersBuilt.HasValue),
                RelicsCaptured = group.Sum(x => (decimal?)x.Statistic.RelicsCaptured),
                RelicsCapturedMatches = group.Count(x => x.Statistic.RelicsCaptured.HasValue),
                TotalScore = group.Average(x => (decimal?)x.Statistic.TotalScore),
                TotalScoreMatches = group.Count(x => x.Statistic.TotalScore.HasValue),
                MilitaryScore = group.Average(x => (decimal?)x.Statistic.MilitaryScore),
                MilitaryScoreMatches = group.Count(x => x.Statistic.MilitaryScore.HasValue),
                EconomyScore = group.Average(x => (decimal?)x.Statistic.EconomyScore),
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
            bool descending = true)
        {
            var available = rows.Where(x => matches(x) > 0 && value(x).HasValue);
            var ordered = descending
                ? available.OrderByDescending(x => value(x)!.Value).ThenBy(x => x.DisplayName)
                : available.OrderBy(x => value(x)!.Value).ThenBy(x => x.DisplayName);
            var entries = ordered.Take(leadersPerBoard)
                .Select((x, index) => new GeneralStatisticEntry(
                    index + 1, x.PlayerId, x.DisplayName, value(x)!.Value, matches(x)))
                .ToArray();
            return new GeneralStatisticBoard(key, category, title, description, valueKind, entries);
        }

        var boards = new[]
        {
            Board("units-killed", "Combate", "O Ceifador", "Maior total de unidades inimigas eliminadas.", GeneralStatisticValueKind.Integer, x => x.UnitsKilled, x => x.UnitsKilledMatches),
            Board("units-lost", "Combate", "O Doador de Experiência", "Maior total de unidades próprias perdidas em batalha.", GeneralStatisticValueKind.Integer, x => x.UnitsLost, x => x.UnitsLostMatches),
            Board("buildings-destroyed", "Combate", "O Demolidor", "Maior total de edifícios inimigos destruídos.", GeneralStatisticValueKind.Integer, x => x.BuildingsDestroyed, x => x.BuildingsDestroyedMatches),
            Board("buildings-lost", "Combate", "O Sem-Teto", "Maior total de edifícios próprios perdidos.", GeneralStatisticValueKind.Integer, x => x.BuildingsLost, x => x.BuildingsLostMatches),
            Board("conversions", "Combate", "O Inquisidor", "Maior total de unidades convertidas por monges.", GeneralStatisticValueKind.Integer, x => x.UnitsConverted, x => x.UnitsConvertedMatches),
            Board("largest-army", "Combate", "O Senhor das Hostes", "Maior exército registrado em uma única partida.", GeneralStatisticValueKind.Integer, x => x.LargestArmy, x => x.LargestArmyMatches),
            Board("food", "Economia", "O Mestre das Provisões", "Maior soma de comida coletada.", GeneralStatisticValueKind.Integer, x => x.FoodCollected, x => x.FoodCollectedMatches),
            Board("wood", "Economia", "O Lenhador-Mor", "Maior soma de madeira coletada.", GeneralStatisticValueKind.Integer, x => x.WoodCollected, x => x.WoodCollectedMatches),
            Board("gold", "Economia", "O Toque de Midas", "Maior soma de ouro coletado.", GeneralStatisticValueKind.Integer, x => x.GoldCollected, x => x.GoldCollectedMatches),
            Board("stone", "Economia", "O Mestre da Pedreira", "Maior soma de pedra coletada.", GeneralStatisticValueKind.Integer, x => x.StoneCollected, x => x.StoneCollectedMatches),
            Board("trade-gold", "Economia", "O Magnata das Rotas", "Maior total de ouro produzido por comércio.", GeneralStatisticValueKind.Integer, x => x.TradeGold, x => x.TradeGoldMatches),
            Board("relic-gold", "Economia", "O Cofre Sagrado", "Maior total de ouro gerado por relíquias.", GeneralStatisticValueKind.Integer, x => x.RelicGold, x => x.RelicGoldMatches),
            Board("research", "Tecnologia", "O Sábio", "Maior total de tecnologias concluídas.", GeneralStatisticValueKind.Integer, x => x.ResearchCount, x => x.ResearchCountMatches),
            Board("explored", "Tecnologia", "O Cartógrafo", "Maior percentual médio do mapa explorado.", GeneralStatisticValueKind.Percentage, x => x.ExploredPercent, x => x.ExploredPercentMatches),
            Board("fastest-feudal", "Tecnologia", "O Precursor", "Menor tempo registrado para chegar à Era Feudal.", GeneralStatisticValueKind.Duration, x => x.FeudalAgeSeconds, x => x.FeudalAgeMatches, false),
            Board("fastest-castle", "Tecnologia", "O Senhor Feudal", "Menor tempo registrado para chegar à Era dos Castelos.", GeneralStatisticValueKind.Duration, x => x.CastleAgeSeconds, x => x.CastleAgeMatches, false),
            Board("fastest-imperial", "Tecnologia", "O Imperador Relâmpago", "Menor tempo registrado para chegar à Era Imperial.", GeneralStatisticValueKind.Duration, x => x.ImperialAgeSeconds, x => x.ImperialAgeMatches, false),
            Board("villagers", "Sociedade", "O Pai da Nação", "Maior pico de aldeões em uma única partida.", GeneralStatisticValueKind.Integer, x => x.PeakVillagers, x => x.PeakVillagersMatches),
            Board("castles", "Sociedade", "O Rei dos Castelos", "Maior total de castelos construídos.", GeneralStatisticValueKind.Integer, x => x.CastlesBuilt, x => x.CastlesBuiltMatches),
            Board("wonders", "Sociedade", "O Arquiteto do Impossível", "Maior total de maravilhas construídas.", GeneralStatisticValueKind.Integer, x => x.WondersBuilt, x => x.WondersBuiltMatches),
            Board("relics", "Sociedade", "O Caçador de Relíquias", "Maior total de relíquias capturadas.", GeneralStatisticValueKind.Integer, x => x.RelicsCaptured, x => x.RelicsCapturedMatches),
            Board("total-score", "Placar", "A Lenda do Nexus", "Maior pontuação total média nas partidas.", GeneralStatisticValueKind.Decimal, x => x.TotalScore, x => x.TotalScoreMatches),
            Board("military-score", "Placar", "O Estrategista", "Maior pontuação militar média.", GeneralStatisticValueKind.Decimal, x => x.MilitaryScore, x => x.MilitaryScoreMatches),
            Board("economy-score", "Placar", "O Grão-Mestre da Economia", "Maior pontuação econômica média.", GeneralStatisticValueKind.Decimal, x => x.EconomyScore, x => x.EconomyScoreMatches)
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
        public int RowCount { get; init; }
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
        public int ExploredPercentMatches { get; init; }
        public decimal? FeudalAgeSeconds { get; init; }
        public int FeudalAgeMatches { get; init; }
        public decimal? CastleAgeSeconds { get; init; }
        public int CastleAgeMatches { get; init; }
        public decimal? ImperialAgeSeconds { get; init; }
        public int ImperialAgeMatches { get; init; }
        public decimal? PeakVillagers { get; init; }
        public int PeakVillagersMatches { get; init; }
        public decimal? CastlesBuilt { get; init; }
        public int CastlesBuiltMatches { get; init; }
        public decimal? WondersBuilt { get; init; }
        public int WondersBuiltMatches { get; init; }
        public decimal? RelicsCaptured { get; init; }
        public int RelicsCapturedMatches { get; init; }
        public decimal? TotalScore { get; init; }
        public int TotalScoreMatches { get; init; }
        public decimal? MilitaryScore { get; init; }
        public int MilitaryScoreMatches { get; init; }
        public decimal? EconomyScore { get; init; }
        public int EconomyScoreMatches { get; init; }
    }
}
