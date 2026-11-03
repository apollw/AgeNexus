namespace AgeNexus.Application.MatchPerformance;

public sealed record AgeExtractorParticipantMapping(
    int PlayerNumber,
    Guid MatchParticipantId);

public sealed record AgeExtractorPlayerAssignmentResult(
    bool Succeeded,
    IReadOnlyDictionary<Guid, ImportedPlayerStatistics>? Players = null,
    string? Error = null);

public static class AgeExtractorPlayerAssignment
{
    public static AgeExtractorPlayerAssignmentResult Assign(
        AgeExtractorJsonImportResult import,
        IReadOnlyCollection<AgeExtractorParticipantMapping> mappings,
        IReadOnlyCollection<Guid> matchParticipantIds)
    {
        if (!import.CanApply || import.PlayerCount != matchParticipantIds.Count ||
            import.Players.Count != matchParticipantIds.Count || mappings.Count != matchParticipantIds.Count ||
            import.Players.Select(x => x.PlayerNumber).Distinct().Count() != import.Players.Count)
        {
            return Failure("A quantidade de jogadores importados não corresponde aos participantes da partida.");
        }

        var participantSet = matchParticipantIds.ToHashSet();
        if (participantSet.Count != matchParticipantIds.Count ||
            mappings.Select(x => x.PlayerNumber).Distinct().Count() != mappings.Count ||
            mappings.Select(x => x.MatchParticipantId).Distinct().Count() != mappings.Count ||
            mappings.Any(x => !participantSet.Contains(x.MatchParticipantId)))
        {
            return Failure("Associe cada posição a um participante diferente da partida.");
        }

        var importedByNumber = import.Players.ToDictionary(x => x.PlayerNumber);
        if (mappings.Any(x => !importedByNumber.ContainsKey(x.PlayerNumber)))
        {
            return Failure("Uma das posições importadas não possui jogador correspondente.");
        }

        return new(true, mappings.ToDictionary(
            x => x.MatchParticipantId,
            x => importedByNumber[x.PlayerNumber]));
    }

    private static AgeExtractorPlayerAssignmentResult Failure(string error) =>
        new(false, Error: error);
}
