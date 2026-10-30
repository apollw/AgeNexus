using AgeNexus.Application.Queries;
using AgeNexus.Domain.GameCatalog;
using AgeNexus.Domain.MatchPerformance;
using AgeNexus.Domain.Matches;
using AgeNexus.Domain.Players;
using AgeNexus.Infrastructure.Persistence;
using AgeNexus.Infrastructure.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgeNexus.Domain.Tests;

public sealed class AiPerformanceRankingTests
{
    [Fact]
    public async Task Ai_ranking_groups_mixed_match_statistics_by_difficulty()
    {
        var options = new DbContextOptionsBuilder<AgeNexusDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var database = new AgeNexusDbContext(options);
        using var memory = new MemoryCache(new MemoryCacheOptions { SizeLimit = 512 });
        var cache = new CompetitionQueryCache(memory, NullLogger<CompetitionQueryCache>.Instance);
        var catalogCache = new CatalogQueryCache(memory, NullLogger<CompetitionQueryCache>.Instance);
        var service = new CompetitionQueryService(database, cache, catalogCache);
        var editionId = Guid.NewGuid();
        var difficulty = new AiDifficulty(Guid.NewGuid(), editionId, "Difícil", 4);
        database.Add(difficulty);
        AddMixedMatch(database, editionId, difficulty.Id, totalScore: 10_000, isMvp: false);
        AddMixedMatch(database, editionId, difficulty.Id, totalScore: 14_000, isMvp: true);
        await database.SaveChangesAsync();

        var ranking = await service.GetAsync(RankingBoard.AiPerformance);

        var entry = Assert.Single(ranking);
        Assert.Equal(difficulty.Id, entry.BeneficiaryId);
        Assert.Equal("IA Difícil", entry.DisplayName);
        Assert.Equal(12_000m, entry.Score);
        Assert.Equal(2, entry.ValidatedMatches);
        Assert.Equal(1, entry.MvpAwards);
    }

    private static void AddMixedMatch(
        AgeNexusDbContext database,
        Guid editionId,
        Guid difficultyId,
        int totalScore,
        bool isMvp)
    {
        var firstHuman = new PlayerProfile(Guid.NewGuid(), $"Humano {Guid.NewGuid():N}");
        var secondHuman = new PlayerProfile(Guid.NewGuid(), $"Humano {Guid.NewGuid():N}");
        var match = new Match(Guid.NewGuid(), editionId, firstHuman.Id, DateTimeOffset.UtcNow,
            MatchType.Mixed, MatchNature.Ranked);
        var firstTeam = match.AddTeam(Guid.NewGuid());
        var secondTeam = match.AddTeam(Guid.NewGuid());
        match.AddParticipant(firstTeam.Id, MatchParticipant.Human(Guid.NewGuid(), firstHuman.Id));
        var aiParticipant = MatchParticipant.Ai(Guid.NewGuid(), difficultyId);
        match.AddParticipant(firstTeam.Id, aiParticipant);
        match.AddParticipant(secondTeam.Id, MatchParticipant.Human(Guid.NewGuid(), secondHuman.Id));
        match.SetTeamResult(firstTeam.Id, TeamResult.Victory);
        match.SetTeamResult(secondTeam.Id, TeamResult.Defeat);
        match.Submit();
        match.RequestConfirmation();
        match.MarkConfirmed();
        match.Validate();

        var report = new MatchStatisticsReport(Guid.NewGuid(), match.Id, firstHuman.Id,
            MatchStatisticsSource.Manual, DateTimeOffset.UtcNow);
        report.Submit(DateTimeOffset.UtcNow, isComplete: true);
        report.MarkConfirmed(DateTimeOffset.UtcNow);
        var statistic = new PlayerMatchStatistics(
            Guid.NewGuid(), report.Id, match.Id, firstTeam.Id, aiParticipant.Id, null, difficultyId,
            StatisticValueOrigin.Manual,
            new MatchStatisticValues(TotalScore: totalScore, IsTeamMvp: isMvp));

        database.AddRange(firstHuman, secondHuman, match, report, statistic);
    }
}
