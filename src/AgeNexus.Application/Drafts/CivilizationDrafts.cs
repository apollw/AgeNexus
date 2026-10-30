using AgeNexus.Domain.Matches;

namespace AgeNexus.Application.Drafts;

public enum CivilizationDraftPhase
{
    Lobby,
    Preferences,
    Bans,
    Picks,
    Review,
    Completed,
    Cancelled
}

public enum CivilizationDraftActionKind
{
    Ban,
    Pick
}

public sealed record CreateCivilizationDraftRequest(
    string Title,
    Guid GameEditionId,
    Guid? MapDefinitionId,
    MatchGameMode GameMode,
    IReadOnlyCollection<CivilizationDraftTeamInput> Teams);

public sealed record CivilizationDraftTeamInput(
    string Name,
    IReadOnlyCollection<Guid> PlayerProfileIds,
    Guid RepresentativePlayerProfileId);

public sealed record CivilizationDraftOperationResult(bool Succeeded, string? ErrorCode = null, string? ShareCode = null)
{
    public static CivilizationDraftOperationResult Success(string? shareCode = null) => new(true, null, shareCode);
    public static CivilizationDraftOperationResult Failure(string errorCode) => new(false, errorCode);
}

public sealed record CivilizationDraftView(
    Guid Id,
    string ShareCode,
    string Title,
    string EditionName,
    string? MapName,
    MatchGameMode GameMode,
    CivilizationDraftPhase Phase,
    Guid CreatedByPlayerProfileId,
    Guid? CurrentPlayerProfileId,
    CivilizationDraftActionKind? CurrentAction,
    DateTimeOffset? TurnDeadlineUtc,
    CivilizationDraftActionView? LastAction,
    IReadOnlyCollection<CivilizationDraftTeamView> Teams,
    IReadOnlyCollection<CivilizationDraftFactionView> Pool,
    int CompletedActions,
    int TotalActions,
    int Version,
    DateTimeOffset UpdatedAtUtc);

public sealed record CivilizationDraftTeamView(
    string Name,
    Guid RepresentativePlayerProfileId,
    string RepresentativeDisplayName,
    bool RepresentativeReady,
    IReadOnlyCollection<CivilizationDraftParticipantView> Players,
    IReadOnlyCollection<CivilizationDraftTeamPickView> Picks);

public sealed record CivilizationDraftTeamPickView(
    Guid FactionId,
    string FactionName,
    string? FactionImageUrl);

public sealed record CivilizationDraftActionView(
    CivilizationDraftActionKind Kind,
    string TeamName,
    string FactionName,
    bool WasAutomatic,
    DateTimeOffset OccurredAtUtc);

public sealed record CivilizationDraftParticipantView(
    Guid PlayerProfileId,
    string DisplayName,
    string? AvatarUrl,
    Guid? PickFactionId,
    string? PickFactionName,
    string? PickFactionImageUrl);

public sealed record CivilizationDraftFactionView(
    Guid Id,
    string Name,
    string? ImageUrl,
    bool IsBanned,
    bool IsPicked,
    string? AssignedPlayerName,
    string? AssignedTeamName,
    string? BannedByTeamName);

public interface ICivilizationDraftService
{
    Task<CivilizationDraftOperationResult> CreateAsync(
        Guid creatorPlayerProfileId,
        CreateCivilizationDraftRequest request,
        CancellationToken cancellationToken = default);

    Task<CivilizationDraftView?> GetAsync(
        string shareCode,
        Guid viewerPlayerProfileId,
        CancellationToken cancellationToken = default);

    Task<CivilizationDraftOperationResult> SetReadyAsync(string shareCode, Guid playerProfileId, bool ready, CancellationToken cancellationToken = default);
    Task<CivilizationDraftOperationResult> StartAsync(string shareCode, Guid organizerPlayerProfileId, CancellationToken cancellationToken = default);
    Task<CivilizationDraftOperationResult> SubmitBanAsync(string shareCode, Guid playerProfileId, Guid factionId, CancellationToken cancellationToken = default);
    Task<CivilizationDraftOperationResult> SubmitPickAsync(string shareCode, Guid playerProfileId, Guid factionId, CancellationToken cancellationToken = default);
    Task<CivilizationDraftOperationResult> CancelAsync(string shareCode, Guid organizerPlayerProfileId, CancellationToken cancellationToken = default);
}

public interface ICivilizationDraftNotifier
{
    event Func<string, Task>? Changed;
    Task NotifyAsync(string shareCode);
}
