using AgeNexus.Application.Queries;
using AgeNexus.Domain.Matches;
using AgeNexus.Domain.MatchPerformance;
using AgeNexus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgeNexus.Infrastructure.Queries;

internal sealed class AchievementQueryService(
    AgeNexusDbContext database,
    CompetitionQueryCache cache) : IAchievementQueryService
{
    public async Task<IReadOnlyCollection<PlayerAchievement>> GetPlayerAchievementsAsync(
        Guid playerId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken);
        if (snapshot.Players.TryGetValue(playerId, out var player))
        {
            return player.Achievements;
        }

        return AchievementCatalog.All
            .Select(definition => new PlayerAchievement(definition, 0m, null))
            .ToArray();
    }

    public async Task<PagedAchievementFeed> GetRecentUnlocksAsync(
        int page = 1,
        int pageSize = 8,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 24);
        var snapshot = await GetSnapshotAsync(cancellationToken);
        var totalItems = snapshot.Feed.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        return new PagedAchievementFeed(
            snapshot.Feed.Skip((page - 1) * pageSize).Take(pageSize).ToArray(),
            page,
            pageSize,
            totalItems);
    }

    private Task<AchievementSnapshot> GetSnapshotAsync(CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync("achievements:snapshot", BuildSnapshotAsync, cancellationToken);

    private async Task<AchievementSnapshot> BuildSnapshotAsync(CancellationToken cancellationToken)
    {
        var rows = await (
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
            orderby player.Id, match.PlayedAtUtc, match.Id
            select new AchievementSourceRow(
                player.Id,
                player.DisplayName,
                player.AvatarUrl,
                match.PlayedAtUtc,
                match.Id,
                team.Result,
                statistic.UnitsKilled ?? 0,
                statistic.BuildingsDestroyed ?? 0,
                statistic.FoodCollected ?? 0,
                statistic.WoodCollected ?? 0,
                statistic.GoldCollected ?? 0,
                statistic.StoneCollected ?? 0,
                statistic.TradeGold ?? 0,
                statistic.RelicGold ?? 0,
                statistic.UnitsConverted ?? 0,
                statistic.ResearchCount ?? 0,
                statistic.CastlesBuilt ?? 0,
                statistic.WondersBuilt ?? 0,
                statistic.RelicsCaptured ?? 0,
                statistic.IsTeamMvp,
                statistic.Survived == true))
            .ToArrayAsync(cancellationToken);

        var players = new Dictionary<Guid, PlayerAchievementSnapshot>();
        var feed = new List<AchievementFeedEntry>();

        foreach (var group in rows.GroupBy(row => row.PlayerId))
        {
            var first = group.First();
            var progress = Enum.GetValues<AchievementMetric>()
                .ToDictionary(metric => metric, _ => 0m);
            var unlockedAt = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);

            foreach (var row in group)
            {
                AddProgress(progress, row);

                foreach (var definition in AchievementCatalog.All)
                {
                    if (!unlockedAt.ContainsKey(definition.Key) &&
                        progress[definition.Metric] >= definition.Target)
                    {
                        unlockedAt[definition.Key] = row.PlayedAtUtc;
                    }
                }
            }

            var achievements = AchievementCatalog.All
                .Select(definition => new PlayerAchievement(
                    definition,
                    progress[definition.Metric],
                    unlockedAt.TryGetValue(definition.Key, out var unlockedDate) ? unlockedDate : null))
                .ToArray();

            players[group.Key] = new PlayerAchievementSnapshot(achievements);
            feed.AddRange(achievements
                .Where(achievement => achievement.UnlockedAtUtc.HasValue)
                .Select(achievement => new AchievementFeedEntry(
                    group.Key,
                    first.DisplayName,
                    first.AvatarUrl,
                    achievement.Definition,
                    achievement.UnlockedAtUtc!.Value)));
        }

        return new AchievementSnapshot(
            players,
            feed.OrderByDescending(entry => entry.UnlockedAtUtc)
                .ThenBy(entry => entry.DisplayName)
                .ThenBy(entry => entry.Definition.Title)
                .ToArray());
    }

    private static void AddProgress(
        IDictionary<AchievementMetric, decimal> progress,
        AchievementSourceRow row)
    {
        progress[AchievementMetric.Matches]++;
        if (row.Result == TeamResult.Victory) progress[AchievementMetric.Victories]++;
        progress[AchievementMetric.UnitsKilled] += row.UnitsKilled;
        progress[AchievementMetric.BuildingsDestroyed] += row.BuildingsDestroyed;
        progress[AchievementMetric.FoodCollected] += row.FoodCollected;
        progress[AchievementMetric.WoodCollected] += row.WoodCollected;
        progress[AchievementMetric.GoldCollected] += row.GoldCollected;
        progress[AchievementMetric.StoneCollected] += row.StoneCollected;
        progress[AchievementMetric.TradeGold] += row.TradeGold;
        progress[AchievementMetric.RelicGold] += row.RelicGold;
        progress[AchievementMetric.UnitsConverted] += row.UnitsConverted;
        progress[AchievementMetric.ResearchCount] += row.ResearchCount;
        progress[AchievementMetric.CastlesBuilt] += row.CastlesBuilt;
        progress[AchievementMetric.WondersBuilt] += row.WondersBuilt;
        progress[AchievementMetric.RelicsCaptured] += row.RelicsCaptured;
        if (row.IsTeamMvp) progress[AchievementMetric.TeamMvpAwards]++;
        if (row.Survived) progress[AchievementMetric.MatchesSurvived]++;
    }

    private sealed record AchievementSourceRow(
        Guid PlayerId,
        string DisplayName,
        string? AvatarUrl,
        DateTimeOffset PlayedAtUtc,
        Guid MatchId,
        TeamResult Result,
        int UnitsKilled,
        int BuildingsDestroyed,
        long FoodCollected,
        long WoodCollected,
        long GoldCollected,
        long StoneCollected,
        long TradeGold,
        long RelicGold,
        int UnitsConverted,
        int ResearchCount,
        int CastlesBuilt,
        int WondersBuilt,
        int RelicsCaptured,
        bool IsTeamMvp,
        bool Survived);

    private sealed record PlayerAchievementSnapshot(
        IReadOnlyCollection<PlayerAchievement> Achievements);

    private sealed record AchievementSnapshot(
        IReadOnlyDictionary<Guid, PlayerAchievementSnapshot> Players,
        IReadOnlyList<AchievementFeedEntry> Feed);
}
