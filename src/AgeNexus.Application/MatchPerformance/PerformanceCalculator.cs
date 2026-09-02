using AgeNexus.Domain.MatchPerformance;
using AgeNexus.Domain.Matches;

namespace AgeNexus.Application.MatchPerformance;

public sealed class PerformanceCalculator : IPerformanceCalculator
{
    public const string CurrentVersion = "2026.09-performance.1";
    private const decimal SharedMvpTolerance = 0.02m;
    private const decimal DefeatHighlightGap = 0.15m;

    public PerformanceCalculation Calculate(PerformanceCalculationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Players.Count < 1 || request.Players.Any(x =>
                x.PlayerProfileId == Guid.Empty || x.TeamId == Guid.Empty ||
                x.MilitaryScore < 0 || x.EconomyScore < 0 || x.TechnologyScore < 0 || x.SocietyScore < 0))
        {
            throw new ArgumentException("Performance calculation requires at least one valid human player.", nameof(request));
        }

        var players = request.Players.ToArray();
        if (players.Select(x => x.PlayerProfileId).Distinct().Count() != players.Length)
        {
            throw new ArgumentException("A player cannot appear twice in a performance calculation.", nameof(request));
        }

        var military = Normalize(players, x => x.MilitaryScore);
        var economy = Normalize(players, x => x.EconomyScore);
        var technology = Normalize(players, x => x.TechnologyScore);
        var society = Normalize(players, x => x.SocietyScore);
        var teamGame = request.LargestHumanTeamSize > 1;
        var weights = teamGame
            ? new[] { 0.40m, 0.30m, 0.10m, 0.20m }
            : new[] { 0.45m, 0.35m, 0.10m, 0.10m };
        var rows = players.Select(player => new MutableResult(
            player,
            military[player.PlayerProfileId],
            economy[player.PlayerProfileId],
            technology[player.PlayerProfileId],
            society[player.PlayerProfileId],
            Round((military[player.PlayerProfileId] * weights[0]) +
                  (economy[player.PlayerProfileId] * weights[1]) +
                  (technology[player.PlayerProfileId] * weights[2]) +
                  (society[player.PlayerProfileId] * weights[3])))).ToArray();

        ApplyMvp(rows, request.Category);
        ApplyDefeatedTeamHighlight(rows, request.Category);
        return new PerformanceCalculation(CurrentVersion, rows.Select(x => x.ToResult()).ToArray());
    }

    private static Dictionary<Guid, decimal> Normalize(
        IReadOnlyCollection<PerformancePlayerInput> players,
        Func<PerformancePlayerInput, int> selector)
    {
        var minimum = players.Min(selector);
        var maximum = players.Max(selector);
        if (minimum == maximum)
        {
            return players.ToDictionary(x => x.PlayerProfileId, _ => 0.50m);
        }

        return players.ToDictionary(
            x => x.PlayerProfileId,
            x => Round((decimal)(selector(x) - minimum) / (maximum - minimum)));
    }

    private static void ApplyMvp(IReadOnlyCollection<MutableResult> rows, MatchScoringCategory category)
    {
        if (rows.Count < 2)
        {
            return;
        }

        var ordered = rows.OrderByDescending(x => x.Overall).ToArray();
        var leaders = ordered.Where(x => ordered[0].Overall - x.Overall <= SharedMvpTolerance).ToArray();
        var eligiblePoints = category switch
        {
            MatchScoringCategory.PurePvp => 2,
            MatchScoringCategory.HybridPvp => 1,
            _ => 0
        };

        if (leaders.Length == 1)
        {
            leaders[0].AwardType = PerformanceAwardType.Mvp;
            leaders[0].BonusPoints = eligiblePoints;
            return;
        }

        foreach (var leader in leaders)
        {
            leader.AwardType = PerformanceAwardType.SharedMvp;
            leader.BonusPoints = eligiblePoints > 0 ? 1 : 0;
        }
    }

    private static void ApplyDefeatedTeamHighlight(
        IReadOnlyCollection<MutableResult> rows,
        MatchScoringCategory category)
    {
        if (category is not (MatchScoringCategory.PurePvp or MatchScoringCategory.HybridPvp))
        {
            return;
        }

        var topOverall = rows.Max(x => x.Overall);
        foreach (var defeatedTeam in rows.Where(x => x.Input.TeamResult == TeamResult.Defeat).GroupBy(x => x.Input.TeamId))
        {
            var candidate = defeatedTeam.OrderByDescending(x => x.Overall).First();
            if (candidate.AwardType != PerformanceAwardType.None)
            {
                continue;
            }

            var leadsPillar = candidate.Military == rows.Max(x => x.Military) ||
                              candidate.Economy == rows.Max(x => x.Economy) ||
                              candidate.Technology == rows.Max(x => x.Technology) ||
                              candidate.Society == rows.Max(x => x.Society);
            if (leadsPillar && candidate.Overall >= 0.55m && topOverall - candidate.Overall <= DefeatHighlightGap)
            {
                candidate.AwardType = PerformanceAwardType.DefeatedTeamHighlight;
                candidate.BonusPoints = 1;
            }
        }
    }

    private static decimal Round(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);

    private sealed class MutableResult(
        PerformancePlayerInput input,
        decimal military,
        decimal economy,
        decimal technology,
        decimal society,
        decimal overall)
    {
        public PerformancePlayerInput Input { get; } = input;
        public decimal Military { get; } = military;
        public decimal Economy { get; } = economy;
        public decimal Technology { get; } = technology;
        public decimal Society { get; } = society;
        public decimal Overall { get; } = overall;
        public PerformanceAwardType AwardType { get; set; }
        public int BonusPoints { get; set; }

        public PlayerPerformanceResult ToResult() => new(
            Input.PlayerProfileId, Input.TeamId, Military, Economy, Technology, Society,
            Overall, AwardType, BonusPoints);
    }
}
