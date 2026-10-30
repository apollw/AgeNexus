using AgeNexus.Application.Drafts;
using AgeNexus.Infrastructure.Drafts;

namespace AgeNexus.Domain.Tests;

public sealed class CivilizationDraftTurnPlannerTests
{
    [Fact]
    public void Builds_ban_ban_pick_pick_for_every_round()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var turns = CivilizationDraftTurnPlanner.Build([first, second], 2, 0);

        Assert.Equal(
            [
                (first, CivilizationDraftActionKind.Ban),
                (second, CivilizationDraftActionKind.Ban),
                (first, CivilizationDraftActionKind.Pick),
                (second, CivilizationDraftActionKind.Pick),
                (first, CivilizationDraftActionKind.Ban),
                (second, CivilizationDraftActionKind.Ban),
                (first, CivilizationDraftActionKind.Pick),
                (second, CivilizationDraftActionKind.Pick)
            ],
            turns.Select(x => (x.RepresentativePlayerProfileId, x.Kind)).ToArray());
    }

    [Fact]
    public void Respects_the_randomly_selected_starting_team()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var turns = CivilizationDraftTurnPlanner.Build([first, second], 1, 1);

        Assert.Equal(second, turns[0].RepresentativePlayerProfileId);
        Assert.Equal(first, turns[1].RepresentativePlayerProfileId);
        Assert.Equal(second, turns[2].RepresentativePlayerProfileId);
        Assert.Equal(first, turns[3].RepresentativePlayerProfileId);
    }
}
