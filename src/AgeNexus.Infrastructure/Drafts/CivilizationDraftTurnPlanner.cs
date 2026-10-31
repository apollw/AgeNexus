using AgeNexus.Application.Drafts;

namespace AgeNexus.Infrastructure.Drafts;

internal static class CivilizationDraftTurnPlanner
{
    internal sealed record Turn(Guid RepresentativePlayerProfileId, CivilizationDraftActionKind Kind);

    internal static IReadOnlyList<Turn> Build(
        IReadOnlyList<Guid> representatives,
        int picksPerTeam,
        int firstTeamIndex,
        CivilizationDraftOpeningAction openingAction = CivilizationDraftOpeningAction.Ban)
    {
        if (representatives.Count != 2 || picksPerTeam < 1 || firstTeamIndex is < 0 or > 1 ||
            !Enum.IsDefined(openingAction))
        {
            throw new ArgumentOutOfRangeException(nameof(firstTeamIndex), "O draft exige duas equipes, ao menos uma rodada e uma equipe inicial válida.");
        }

        var secondTeamIndex = 1 - firstTeamIndex;
        var secondAction = openingAction == CivilizationDraftOpeningAction.Ban
            ? CivilizationDraftActionKind.Pick
            : CivilizationDraftActionKind.Ban;
        var firstAction = openingAction == CivilizationDraftOpeningAction.Ban
            ? CivilizationDraftActionKind.Ban
            : CivilizationDraftActionKind.Pick;
        var turns = new List<Turn>(picksPerTeam * 4);
        for (var round = 0; round < picksPerTeam; round++)
        {
            turns.Add(new Turn(representatives[firstTeamIndex], firstAction));
            turns.Add(new Turn(representatives[secondTeamIndex], firstAction));
            turns.Add(new Turn(representatives[firstTeamIndex], secondAction));
            turns.Add(new Turn(representatives[secondTeamIndex], secondAction));
        }

        return turns;
    }
}
