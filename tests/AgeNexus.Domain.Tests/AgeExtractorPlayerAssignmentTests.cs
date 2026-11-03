using AgeNexus.Application.MatchPerformance;
using AgeNexus.Domain.MatchPerformance;

namespace AgeNexus.Domain.Tests;

public sealed class AgeExtractorPlayerAssignmentTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    public void Assigns_every_imported_player_to_the_selected_participant(int playerCount)
    {
        var participantIds = Enumerable.Range(0, playerCount).Select(_ => Guid.NewGuid()).ToArray();
        var players = Enumerable.Range(1, playerCount)
            .Select(number => new ImportedPlayerStatistics(
                number,
                new MatchStatisticValues(TotalScore: number * 1_000)))
            .ToArray();
        var import = new AgeExtractorJsonImportResult(
            JsonImportStatus.Valid, playerCount, players, [], []);
        var mappings = Enumerable.Range(1, playerCount)
            .Select(number => new AgeExtractorParticipantMapping(number, participantIds[number - 1]))
            .ToArray();

        var result = AgeExtractorPlayerAssignment.Assign(import, mappings, participantIds);

        Assert.True(result.Succeeded);
        Assert.Equal(playerCount, result.Players!.Count);
        for (var index = 0; index < playerCount; index++)
        {
            Assert.Equal((index + 1) * 1_000, result.Players[participantIds[index]].Values.TotalScore);
        }
    }

    [Fact]
    public void Rejects_duplicate_participant_mappings_instead_of_partially_filling_the_form()
    {
        var participantIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var players = Enumerable.Range(1, 4)
            .Select(number => new ImportedPlayerStatistics(number, new MatchStatisticValues(TotalScore: number)))
            .ToArray();
        var import = new AgeExtractorJsonImportResult(JsonImportStatus.Valid, 4, players, [], []);
        var mappings = new[]
        {
            new AgeExtractorParticipantMapping(1, participantIds[0]),
            new AgeExtractorParticipantMapping(2, participantIds[1]),
            new AgeExtractorParticipantMapping(3, participantIds[1]),
            new AgeExtractorParticipantMapping(4, participantIds[3])
        };

        var result = AgeExtractorPlayerAssignment.Assign(import, mappings, participantIds);

        Assert.False(result.Succeeded);
        Assert.Null(result.Players);
    }
}
