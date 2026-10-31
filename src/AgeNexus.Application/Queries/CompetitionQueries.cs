using AgeNexus.Domain.Matches;
using AgeNexus.Domain.MatchPerformance;

namespace AgeNexus.Application.Queries;

public enum RankingBoard
{
    GeneralCompetitive,
    Career,
    AiPerformance,
    TeamLineup,
    ClanCompetitive
}

public sealed record RankingEntry(
    int Position,
    Guid BeneficiaryId,
    string DisplayName,
    decimal Score,
    int ValidatedMatches,
    bool IsProvisional)
{
    public string? AvatarUrl { get; init; }
    public int MvpAwards { get; init; }
}

public interface IRankingQueryService
{
    Task<IReadOnlyCollection<RankingEntry>> GetAsync(
        RankingBoard board,
        Guid? seasonId = null,
        int limit = 100,
        CancellationToken cancellationToken = default);

    Task<RankingDashboard> GetDashboardAsync(
        Guid? seasonId = null,
        int limit = 100,
        CancellationToken cancellationToken = default);
}

public sealed record RankingDashboard(
    IReadOnlyCollection<RankingEntry> GeneralCompetitive,
    IReadOnlyCollection<RankingEntry> Career,
    IReadOnlyCollection<RankingEntry> AiPerformance,
    IReadOnlyCollection<RankingEntry> TeamLineup,
    IReadOnlyCollection<RankingEntry> ClanCompetitive);

public sealed record MatchSummary(
    Guid MatchId,
    Guid CreatedByPlayerProfileId,
    DateTimeOffset PlayedAtUtc,
    MatchScoringCategory Category,
    string Format,
    MatchStatus Status,
    IReadOnlyCollection<MatchTeamSummary> Teams)
{
    public AgeNexus.Domain.Matches.MatchType Type { get; init; }
    public Guid? MapDefinitionId { get; init; }
    public string? MapName { get; init; }
    public MatchGameMode GameMode { get; init; }
    public Guid? CreatedByApplicationUserId { get; init; }
    public string? YouTubeVideoUrl { get; init; }
    public int ScreenshotCount { get; init; }
    public bool HasReplay { get; init; }
    public MatchStatisticsStatus? StatisticsStatus { get; init; }
    public Guid? StatisticsSubmittedByPlayerProfileId { get; init; }
    public Guid? StatisticsSubmittedByApplicationUserId { get; init; }
    public string? StatisticsSubmittedByDisplayName { get; init; }
}

public sealed record MatchTeamSummary(
    Guid TeamId,
    int Position,
    TeamResult Result,
    IReadOnlyCollection<string> Participants);

public sealed record MatchHistoryFilter(
    int Page = 1,
    int PageSize = 10,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    Guid? PlayerId = null,
    bool PendingStatisticsReview = false,
    Guid? OpponentPlayerId = null,
    Guid? MapDefinitionId = null,
    MatchGameMode? GameMode = null);

public sealed record MatchMetadataEditView(
    Guid MatchId,
    Guid GameEditionId,
    string GameEditionName,
    Guid? MapDefinitionId,
    MatchGameMode GameMode);

public sealed record PagedMatchHistory(
    IReadOnlyCollection<MatchSummary> Items,
    int Page,
    int PageSize,
    int TotalItems)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalItems / (double)PageSize));
}

public interface IMatchHistoryQueryService
{
    Task<IReadOnlyCollection<CatalogOption>> GetPlayerOptionsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CatalogOption>> GetMapOptionsAsync(
        CancellationToken cancellationToken = default);

    Task<MatchMetadataEditView?> GetMetadataForEditAsync(
        Guid matchId,
        CancellationToken cancellationToken = default);

    Task<PagedMatchHistory> GetPageAsync(
        MatchHistoryFilter filter,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<MatchSummary>> GetRecentAsync(
        int limit = 50,
        CancellationToken cancellationToken = default);
}

public sealed record PlayerSummary(
    Guid PlayerId,
    string DisplayName,
    string? AvatarUrl,
    decimal CompetitiveRating,
    decimal CareerPoints);

public interface IPlayerDirectoryQueryService
{
    Task<IReadOnlyCollection<PlayerSummary>> GetAsync(
        int limit = 100,
        CancellationToken cancellationToken = default);
}

public sealed record PlayerPublicFactionSummary(
    Guid FactionId,
    string Name,
    string? ImageUrl,
    int Uses,
    int Victories,
    decimal WinRate);

public sealed record PlayerPublicProfileDashboard(
    Guid PlayerId,
    string DisplayName,
    string? Bio,
    string? Location,
    string? AvatarUrl,
    string? FavoriteFactionName,
    string? FavoriteFactionImageUrl,
    decimal CompetitiveRating,
    decimal CareerPoints,
    int ValidatedMatches,
    int Victories,
    int Draws,
    int Defeats,
    decimal WinRate,
    IReadOnlyCollection<PlayerPublicFactionSummary> Factions);

public interface IPlayerPublicProfileQueryService
{
    Task<PlayerPublicProfileDashboard?> GetAsync(
        Guid playerId,
        CancellationToken cancellationToken = default);
}

public enum GeneralStatisticValueKind
{
    Integer,
    Decimal,
    Percentage,
    Duration
}

public sealed record GeneralStatisticEntry(
    int Position,
    Guid PlayerId,
    string DisplayName,
    string? AvatarUrl,
    decimal Value,
    int Matches,
    decimal? AbsoluteValue = null,
    decimal? AverageValue = null);

public sealed record GeneralStatisticBoard(
    string Key,
    string Category,
    string Title,
    string Description,
    GeneralStatisticValueKind ValueKind,
    IReadOnlyCollection<GeneralStatisticEntry> Entries,
    bool IsNegative = false,
    GeneralStatisticValueKind? AverageValueKind = null,
    string AbsoluteLabel = "Total",
    string AverageLabel = "Média por partida");

public sealed record GeneralStatisticsDashboard(
    int MatchesWithStatistics,
    int PlayersWithStatistics,
    int StatisticRows,
    IReadOnlyCollection<GeneralStatisticBoard> Boards);

public interface IGeneralStatisticsQueryService
{
    Task<GeneralStatisticsDashboard> GetAsync(
        int leadersPerBoard = 5,
        CancellationToken cancellationToken = default);
}

public sealed record FactionStatistics(
    Guid FactionId,
    string Name,
    string? ImageUrl,
    int Uses,
    int Victories,
    int Draws,
    int Defeats,
    decimal WinRate,
    decimal StrengthIndex);

public static class FactionRankingCalculator
{
    private const double ConfidenceLevel = 1.96;

    public static decimal CalculateStrengthIndex(int uses, int victories, int draws)
    {
        if (uses <= 0 || victories < 0 || draws < 0 || victories + draws > uses)
        {
            return 0m;
        }

        var performance = (victories + draws * 0.5d) / uses;
        var confidenceSquared = ConfidenceLevel * ConfidenceLevel;
        var adjustedCenter = performance + confidenceSquared / (2d * uses);
        var adjustedMargin = ConfidenceLevel * Math.Sqrt(
            (performance * (1d - performance) + confidenceSquared / (4d * uses)) / uses);
        var lowerBound = (adjustedCenter - adjustedMargin) / (1d + confidenceSquared / uses);

        return Math.Round((decimal)(lowerBound * 100d), 2);
    }
}

public sealed record PlayerFactionStatistics(
    Guid PlayerId,
    Guid FactionId,
    string FactionName,
    int Uses,
    int Victories,
    decimal WinRate);

public interface IStatisticsQueryService
{
    Task<IReadOnlyCollection<FactionStatistics>> GetFactionStatisticsAsync(
        Guid? gameEditionId = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<PlayerFactionStatistics>> GetPlayerFactionStatisticsAsync(
        Guid playerId,
        CancellationToken cancellationToken = default);
}

public sealed record ClanMemberSummary(Guid PlayerProfileId, string DisplayName, string? AvatarUrl);

public sealed record ClanSummary(
    Guid ClanId,
    string Name,
    string Tag,
    IReadOnlyCollection<ClanMemberSummary> Members)
{
    public int ActiveMembers => Members.Count;
}

public interface IClanQueryService
{
    Task<IReadOnlyCollection<ClanSummary>> GetAsync(CancellationToken cancellationToken = default);
}

public interface IClanService
{
    Task<ClanOperationResult> CreateAsync(
        Guid createdByPlayerProfileId,
        string name,
        string tag,
        IReadOnlyCollection<Guid> memberPlayerProfileIds,
        CancellationToken cancellationToken = default);
}

public sealed record ClanOperationResult(bool Succeeded, Guid? ClanId = null, string? ErrorCode = null)
{
    public static ClanOperationResult Success(Guid clanId) => new(true, clanId);
    public static ClanOperationResult Failure(string errorCode) => new(false, null, errorCode);
}

public sealed record CatalogOption(Guid Id, string Name);

public sealed record MatchRegistrationCatalog(
    IReadOnlyCollection<CatalogOption> GameEditions,
    IReadOnlyCollection<CatalogOption> Players,
    IReadOnlyCollection<CatalogOption> Factions,
    IReadOnlyCollection<CatalogOption> Maps,
    IReadOnlyCollection<CatalogOption> AiDifficulties,
    IReadOnlyCollection<CatalogOption> Seasons,
    IReadOnlyCollection<CatalogOption> Patches,
    IReadOnlyCollection<ClanSummary> Clans);

public interface ICatalogQueryService
{
    Task<MatchRegistrationCatalog> GetMatchRegistrationCatalogAsync(
        Guid? gameEditionId = null,
        CancellationToken cancellationToken = default);
}
