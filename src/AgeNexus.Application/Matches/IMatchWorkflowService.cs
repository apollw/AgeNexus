using AgeNexus.Domain.EvidenceAndModeration;
using AgeNexus.Domain.Matches;

namespace AgeNexus.Application.Matches;

public interface IMatchWorkflowService
{
    Task<MatchWorkflowResult> RegisterAsync(RegisterMatchRequest request, CancellationToken cancellationToken = default);
    Task<MatchWorkflowResult> ConfirmAsync(
        Guid matchId,
        Guid playerProfileId,
        ConfirmationDecision decision,
        string? comment,
        CancellationToken cancellationToken = default);
    Task<MatchWorkflowResult> DecideEvidenceAsync(
        Guid matchId,
        VerificationStatus status,
        string reason,
        Guid? decidedByApplicationUserId,
        CancellationToken cancellationToken = default);
    Task<MatchWorkflowResult> ValidateAsync(Guid matchId, CancellationToken cancellationToken = default);
    Task<MatchWorkflowResult> VoidAsync(
        Guid matchId,
        Guid changedByApplicationUserId,
        string reason,
        CancellationToken cancellationToken = default);
    Task<MatchWorkflowResult> DeleteAsync(
        Guid matchId,
        Guid requestedByPlayerProfileId,
        CancellationToken cancellationToken = default);
}

public sealed record RegisterMatchRequest(
    Guid GameEditionId,
    Guid CreatedByPlayerProfileId,
    DateTimeOffset PlayedAtUtc,
    AgeNexus.Domain.Matches.MatchType Type,
    MatchNature Nature,
    Guid? SeasonId,
    Guid? MapDefinitionId,
    Guid? GamePatchId,
    IReadOnlyCollection<RegisterMatchTeam> Teams);

public sealed record RegisterMatchTeam(
    TeamResult Result,
    IReadOnlyCollection<RegisterMatchParticipant> Participants);

public sealed record RegisterMatchParticipant(
    ParticipantType Type,
    Guid? PlayerProfileId,
    Guid? AiDifficultyId,
    Guid? FactionId,
    FactionSelection FactionSelection);

public sealed record MatchWorkflowResult(bool Succeeded, Guid MatchId, string? ErrorCode = null, bool AlreadyApplied = false)
{
    public static MatchWorkflowResult Success(Guid matchId, bool alreadyApplied = false) =>
        new(true, matchId, null, alreadyApplied);

    public static MatchWorkflowResult Failure(Guid matchId, string errorCode) =>
        new(false, matchId, errorCode);
}
