using AgeNexus.Application.Drafts;

namespace AgeNexus.Infrastructure.Drafts;

internal static class CivilizationDraftTurnPlanner
{
    internal sealed record Turn(Guid RepresentativePlayerProfileId, CivilizationDraftActionKind Kind);

    internal static IReadOnlyList<Turn> Build(
        IReadOnlyList<Guid> representatives,
        int picksPerTeam,
        int firstTeamIndex)
    {
        if (representatives.Count != 2 || picksPerTeam < 1 || firstTeamIndex is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(firstTeamIndex), "O draft exige duas equipes, ao menos uma rodada e uma equipe inicial válida.");
        }

        var secondTeamIndex = 1 - firstTeamIndex;
        var turns = new List<Turn>(picksPerTeam * 4);
        for (var round = 0; round < picksPerTeam; round++)
        {
            turns.Add(new Turn(representatives[firstTeamIndex], CivilizationDraftActionKind.Ban));
            turns.Add(new Turn(representatives[secondTeamIndex], CivilizationDraftActionKind.Ban));
            turns.Add(new Turn(representatives[firstTeamIndex], CivilizationDraftActionKind.Pick));
            turns.Add(new Turn(representatives[secondTeamIndex], CivilizationDraftActionKind.Pick));
        }

        return turns;
    }
}
