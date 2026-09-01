using AgeNexus.Domain.Competition;

namespace AgeNexus.Domain.Tests;

public sealed class TeamLineupTests
{
    [Fact]
    public void Identity_is_stable_regardless_of_member_order()
    {
        var editionId = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var lineupA = TeamLineup.Create(editionId, [first, second], DateTimeOffset.UtcNow);
        var lineupB = TeamLineup.Create(editionId, [second, first], DateTimeOffset.UtcNow);

        Assert.Equal(lineupA.Id, lineupB.Id);
        Assert.Equal(lineupA.NormalizedKey, lineupB.NormalizedKey);
    }
}
