using AgeNexus.Application.Drafts;
using AgeNexus.Infrastructure.Drafts;

namespace AgeNexus.Domain.Tests;

public sealed class CivilizationDraftRockPaperScissorsTests
{
    [Theory]
    [InlineData(CivilizationDraftRpsChoice.Rock, CivilizationDraftRpsChoice.Scissors)]
    [InlineData(CivilizationDraftRpsChoice.Paper, CivilizationDraftRpsChoice.Rock)]
    [InlineData(CivilizationDraftRpsChoice.Scissors, CivilizationDraftRpsChoice.Paper)]
    public void First_representative_wins_with_the_stronger_choice(
        CivilizationDraftRpsChoice first,
        CivilizationDraftRpsChoice second)
    {
        Assert.Equal(0, CivilizationDraftRockPaperScissors.Winner(first, second));
        Assert.Equal(1, CivilizationDraftRockPaperScissors.Winner(second, first));
    }

    [Theory]
    [InlineData(CivilizationDraftRpsChoice.Rock)]
    [InlineData(CivilizationDraftRpsChoice.Paper)]
    [InlineData(CivilizationDraftRpsChoice.Scissors)]
    public void Equal_choices_are_a_tie(CivilizationDraftRpsChoice choice)
    {
        Assert.Null(CivilizationDraftRockPaperScissors.Winner(choice, choice));
    }
}
