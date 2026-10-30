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

public sealed record CreateCivilizationDraftRequest(
    string Title,
    Guid GameEditionId,
    Guid? MapDefinitionId,
    MatchGameMode GameMode,
    IReadOnlyCollection<CivilizationDraftTeamInput> Teams);

public sealed record CivilizationDraftTeamInput(string Name, IReadOnlyCollection<Guid> PlayerProfileIds);

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
    IReadOnlyCollection<CivilizationDraftTeamView> Teams,
    IReadOnlyCollection<CivilizationDraftFactionView> Pool,
    IReadOnlyCollection<Guid> MyPreferenceFactionIds,
    IReadOnlyCollection<Guid> RevealedBanFactionIds,
    int CompletedActions,
    int TotalActions,
    int Version,
    DateTimeOffset UpdatedAtUtc);

public sealed record CivilizationDraftTeamView(
    string Name,
    IReadOnlyCollection<CivilizationDraftParticipantView> Players);

public sealed record CivilizationDraftParticipantView(
    Guid PlayerProfileId,
    string DisplayName,
    string? AvatarUrl,
    bool Ready,
    bool FinalConfirmed,
    Guid? PickFactionId,
    string? PickFactionName,
    string? PickFactionImageUrl,
    bool PickWasProtected);

public sealed record CivilizationDraftFactionView(
    Guid Id,
    string Name,
    string? ImageUrl,
    bool IsBanned,
    bool IsProtected,
    bool IsUnavailable);

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
    Task<CivilizationDraftOperationResult> SubmitPreferencesAsync(string shareCode, Guid playerProfileId, IReadOnlyCollection<Guid> factionIds, CancellationToken cancellationToken = default);
    Task<CivilizationDraftOperationResult> SubmitBanAsync(string shareCode, Guid playerProfileId, Guid factionId, CancellationToken cancellationToken = default);
    Task<CivilizationDraftOperationResult> SubmitPickAsync(string shareCode, Guid playerProfileId, Guid factionId, CancellationToken cancellationToken = default);
    Task<CivilizationDraftOperationResult> ConfirmFinalAsync(string shareCode, Guid playerProfileId, CancellationToken cancellationToken = default);
    Task<CivilizationDraftOperationResult> CancelAsync(string shareCode, Guid organizerPlayerProfileId, CancellationToken cancellationToken = default);
}

public interface ICivilizationDraftNotifier
{
    event Func<string, Task>? Changed;
    Task NotifyAsync(string shareCode);
}
