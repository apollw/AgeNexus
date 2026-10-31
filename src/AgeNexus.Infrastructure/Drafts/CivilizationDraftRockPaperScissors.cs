using AgeNexus.Application.Drafts;

namespace AgeNexus.Infrastructure.Drafts;

internal static class CivilizationDraftRockPaperScissors
{
    internal static int? Winner(
        CivilizationDraftRpsChoice first,
        CivilizationDraftRpsChoice second)
    {
        if (first == second)
        {
            return null;
        }

        return (first, second) switch
        {
            (CivilizationDraftRpsChoice.Rock, CivilizationDraftRpsChoice.Scissors) or
            (CivilizationDraftRpsChoice.Paper, CivilizationDraftRpsChoice.Rock) or
            (CivilizationDraftRpsChoice.Scissors, CivilizationDraftRpsChoice.Paper) => 0,
            _ => 1
        };
    }
}
