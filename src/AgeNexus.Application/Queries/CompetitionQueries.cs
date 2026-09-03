namespace AgeNexus.Application.Queries;

public enum RankingBoard
{
    GeneralCompetitive,
    Career,
    Pve,
    TeamLineup,
    ClanCompetitive,
    ClanPve
}

public sealed record RankingEntry(
    int Position,
    Guid BeneficiaryId,
    string DisplayName,
    decimal Score,
    int ValidatedMatches,
    bool IsProvisional);

public interface IRankingQueryService
{
    Task<IReadOnlyCollection<RankingEntry>> GetAsync(
        RankingBoard board,
        Guid? seasonId = null,
        int limit = 100,
        CancellationToken cancellationToken = default);
}

public sealed record MatchSummary(
    Guid MatchId,
    DateTimeOffset PlayedAtUtc,
    string Category,
    string Format,
    string Status,
    IReadOnlyCollection<string> Teams);

public interface IMatchHistoryQueryService
{
    Task<IReadOnlyCollection<MatchSummary>> GetRecentAsync(
        int limit = 50,
        CancellationToken cancellationToken = default);
}

public sealed record PlayerSummary(
    Guid PlayerId,
    string DisplayName,
    string? AvatarUrl,
    decimal CompetitiveRating,
    decimal CareerPoints,
    decimal PvePoints);

public interface IPlayerDirectoryQueryService
{
    Task<IReadOnlyCollection<PlayerSummary>> GetAsync(
        int limit = 100,
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
    decimal Value,
    int Matches);

public sealed record GeneralStatisticBoard(
    string Key,
    string Category,
    string Title,
    string Description,
    GeneralStatisticValueKind ValueKind,
    IReadOnlyCollection<GeneralStatisticEntry> Entries);

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
    decimal WinRate);

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

public sealed record ClanSummary(Guid ClanId, string Name, string Tag, int ActiveMembers);

public interface IClanQueryService
{
    Task<IReadOnlyCollection<ClanSummary>> GetAsync(CancellationToken cancellationToken = default);
}

public sealed record CatalogOption(Guid Id, string Name);

public sealed record MatchRegistrationCatalog(
    IReadOnlyCollection<CatalogOption> GameEditions,
    IReadOnlyCollection<CatalogOption> Players,
    IReadOnlyCollection<CatalogOption> Factions,
    IReadOnlyCollection<CatalogOption> Maps,
    IReadOnlyCollection<CatalogOption> AiDifficulties,
    IReadOnlyCollection<CatalogOption> Seasons,
    IReadOnlyCollection<CatalogOption> Patches);

public interface ICatalogQueryService
{
    Task<MatchRegistrationCatalog> GetMatchRegistrationCatalogAsync(
        Guid? gameEditionId = null,
        CancellationToken cancellationToken = default);
}
