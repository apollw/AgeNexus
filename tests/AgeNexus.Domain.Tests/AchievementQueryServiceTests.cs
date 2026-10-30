using AgeNexus.Application.Queries;
using AgeNexus.Domain.Matches;
using AgeNexus.Domain.MatchPerformance;
using AgeNexus.Domain.Players;
using AgeNexus.Infrastructure.Persistence;
using AgeNexus.Infrastructure.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgeNexus.Domain.Tests;

public sealed class AchievementQueryServiceTests
{
    [Fact]
    public async Task Achievements_are_rebuilt_from_confirmed_statistics_only()
    {
        var options = new DbContextOptionsBuilder<AgeNexusDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var database = new AgeNexusDbContext(options);
        using var memory = new MemoryCache(new MemoryCacheOptions { SizeLimit = 512 });
        var cache = new CompetitionQueryCache(memory, NullLogger<CompetitionQueryCache>.Instance);
        var service = new AchievementQueryService(database, cache);
        var player = new PlayerProfile(Guid.NewGuid(), "Conquistador");
        var opponent = new PlayerProfile(Guid.NewGuid(), "Oponente");
        database.AddRange(player, opponent);

        var unlockedAt = new DateTimeOffset(2026, 10, 1, 20, 0, 0, TimeSpan.Zero);
        AddMatchWithStatistics(database, player, opponent, unlockedAt, confirmed: true,
            new MatchStatisticValues(UnitsKilled: 1_000, BuildingsDestroyed: 500));
        AddMatchWithStatistics(database, player, opponent, unlockedAt.AddDays(1), confirmed: false,
            new MatchStatisticValues(UnitsKilled: 10_000, BuildingsDestroyed: 10_000));
        await database.SaveChangesAsync();

        var achievements = await service.GetPlayerAchievementsAsync(player.Id);

        Assert.Equal(70, achievements.Count);
        Assert.Equal(unlockedAt, achievements.Single(x => x.Definition.Key == "first-battle").UnlockedAtUtc);
        Assert.Equal(unlockedAt, achievements.Single(x => x.Definition.Key == "thousand-kills").UnlockedAtUtc);
        Assert.Equal(1_000m, achievements.Single(x => x.Definition.Key == "five-thousand-kills").Progress);
        Assert.False(achievements.Single(x => x.Definition.Key == "five-thousand-kills").IsUnlocked);
        Assert.Equal(1_000m, achievements.Single(x => x.Definition.Key == "twenty-thousand-kills").Progress);
        Assert.False(achievements.Single(x => x.Definition.Key == "twenty-thousand-kills").IsUnlocked);
        Assert.Equal(unlockedAt, achievements.Single(x => x.Definition.Key == "five-hundred-buildings").UnlockedAtUtc);

        var feed = await service.GetRecentUnlocksAsync(page: 1, pageSize: 2);
        Assert.Equal(2, feed.Items.Count);
        Assert.All(feed.Items, entry => Assert.Equal(player.Id, entry.PlayerId));
    }

    private static void AddMatchWithStatistics(
        AgeNexusDbContext database,
        PlayerProfile player,
        PlayerProfile opponent,
        DateTimeOffset playedAtUtc,
        bool confirmed,
        MatchStatisticValues values)
    {
        var match = new Match(Guid.NewGuid(), Guid.NewGuid(), player.Id, playedAtUtc,
            MatchType.PlayerVersusPlayer, MatchNature.Casual);
        var winner = match.AddTeam(Guid.NewGuid());
        var loser = match.AddTeam(Guid.NewGuid());
        var participant = MatchParticipant.Human(Guid.NewGuid(), player.Id);
        match.AddParticipant(winner.Id, participant);
        match.AddParticipant(loser.Id, MatchParticipant.Human(Guid.NewGuid(), opponent.Id));
        match.SetTeamResult(winner.Id, TeamResult.Victory);
        match.SetTeamResult(loser.Id, TeamResult.Defeat);
        match.Submit();
        match.RequestConfirmation();
        match.MarkConfirmed();
        match.Validate();

        var report = new MatchStatisticsReport(Guid.NewGuid(), match.Id, player.Id,
            MatchStatisticsSource.Manual, playedAtUtc);
        report.Submit(playedAtUtc, isComplete: true);
        if (confirmed) report.MarkConfirmed(playedAtUtc);

        var statistic = new PlayerMatchStatistics(Guid.NewGuid(), report.Id, match.Id, winner.Id,
            participant.Id, player.Id, null, StatisticValueOrigin.Manual, values);
        database.AddRange(match, report, statistic);
    }
}
