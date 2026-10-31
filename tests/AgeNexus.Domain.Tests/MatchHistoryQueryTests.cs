using AgeNexus.Application.Queries;
using AgeNexus.Domain.EvidenceAndModeration;
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

public sealed class MatchHistoryQueryTests
{
    [Fact]
    public async Task Page_keeps_summary_participants_evidence_and_report_metadata()
    {
        var options = new DbContextOptionsBuilder<AgeNexusDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var database = new AgeNexusDbContext(options);
        using var memory = new MemoryCache(new MemoryCacheOptions { SizeLimit = 512 });
        var logger = NullLogger<CompetitionQueryCache>.Instance;
        var service = new CompetitionQueryService(
            database,
            new CompetitionQueryCache(memory, logger),
            new CatalogQueryCache(memory, logger));

        var editionId = Guid.NewGuid();
        var map = new MapDefinition(Guid.NewGuid(), editionId, "Arena", "arena");
        var firstFaction = new Faction(Guid.NewGuid(), editionId, "Mongóis", "mongols");
        var secondFaction = new Faction(Guid.NewGuid(), editionId, "Romanos", "romans", "https://example.test/romans.png");
        var firstPlayer = new PlayerProfile(Guid.NewGuid(), "Primeiro");
        var secondPlayer = new PlayerProfile(Guid.NewGuid(), "Segundo");
        var match = new Match(
            Guid.NewGuid(), editionId, firstPlayer.Id, DateTimeOffset.UtcNow,
            MatchType.PlayerVersusPlayer, MatchNature.Ranked);
        var firstTeam = match.AddTeam(Guid.NewGuid());
        var secondTeam = match.AddTeam(Guid.NewGuid());
        match.AddParticipant(firstTeam.Id, MatchParticipant.Human(Guid.NewGuid(), firstPlayer.Id, firstFaction.Id));
        match.AddParticipant(secondTeam.Id, MatchParticipant.Human(Guid.NewGuid(), secondPlayer.Id, secondFaction.Id));
        match.SetTeamResult(firstTeam.Id, TeamResult.Victory);
        match.SetTeamResult(secondTeam.Id, TeamResult.Defeat);
        match.SetCatalogContext(null, map.Id, null);
        match.SetGameMode(MatchGameMode.Regicide);
        match.Submit();

        var report = new MatchStatisticsReport(
            Guid.NewGuid(), match.Id, firstPlayer.Id,
            MatchStatisticsSource.Manual, DateTimeOffset.UtcNow);
        var video = new MatchEvidence(
            Guid.NewGuid(), match.Id, firstPlayer.Id, EvidenceKind.VideoLink,
            DateTimeOffset.UtcNow, externalUrl: "https://example.com/video");
        var screenshot = new MatchEvidence(
            Guid.NewGuid(), match.Id, firstPlayer.Id, EvidenceKind.ResultScreenshot,
            DateTimeOffset.UtcNow, objectKey: "matches/screenshot.png");
        database.AddRange(map, firstFaction, secondFaction, firstPlayer, secondPlayer, match, report, video, screenshot);
        await database.SaveChangesAsync();

        var page = await service.GetPageAsync(new MatchHistoryFilter(PageSize: 10));

        var summary = Assert.Single(page.Items);
        Assert.Equal("Arena", summary.MapName);
        Assert.Equal(MatchGameMode.Regicide, summary.GameMode);
        Assert.Equal("https://example.com/video", summary.YouTubeVideoUrl);
        Assert.Equal(1, summary.ScreenshotCount);
        Assert.Equal(MatchStatisticsStatus.Draft, summary.StatisticsStatus);
        var participants = summary.Teams.SelectMany(x => x.Participants).ToArray();
        Assert.Equal(["Primeiro", "Segundo"], participants.Select(x => x.DisplayName).ToArray());
        Assert.Equal(["Mongóis", "Romanos"], participants.Select(x => x.FactionName).ToArray());
        Assert.Equal("/images/civilizations/mongols.png", participants[0].FactionImageUrl);
        Assert.Equal("https://example.test/romans.png", participants[1].FactionImageUrl);
    }
}
