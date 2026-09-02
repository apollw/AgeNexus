using AgeNexus.Domain.MatchPerformance;
using AgeNexus.Domain.Matches;

namespace AgeNexus.Application.MatchPerformance;

public interface IPerformanceStatisticsService
{
    Task<PerformanceReportView?> GetAsync(Guid matchId, CancellationToken cancellationToken = default);
    Task<PerformanceOperationResult> SaveManualAsync(
        SavePerformanceReportRequest request,
        CancellationToken cancellationToken = default);
    Task<PerformanceOperationResult> ImportReplayAsync(
        ImportReplayRequest request,
        CancellationToken cancellationToken = default);
    Task<PerformanceOperationResult> SubmitAsync(
        Guid reportId,
        Guid playerProfileId,
        CancellationToken cancellationToken = default);
    Task<PerformanceOperationResult> ConfirmAsync(
        Guid reportId,
        Guid playerProfileId,
        StatisticsConfirmationDecision decision,
        CancellationToken cancellationToken = default);
    Task<PerformanceOperationResult> FinalizeAsync(
        Guid reportId,
        CancellationToken cancellationToken = default);
}

public sealed record SavePerformanceReportRequest(
    Guid MatchId,
    Guid SubmittedByPlayerProfileId,
    MatchStatisticsSource Source,
    IReadOnlyCollection<SavePlayerStatistics> Players);

public sealed record SavePlayerStatistics(
    Guid PlayerProfileId,
    MatchStatisticValues Values,
    StatisticValueOrigin Origin = StatisticValueOrigin.Manual);

public sealed record ImportReplayRequest(
    Guid MatchId,
    Guid SubmittedByPlayerProfileId,
    string FileName,
    byte[] Content);

public sealed record PerformanceOperationResult(
    bool Succeeded,
    Guid? ReportId = null,
    string? ErrorCode = null,
    bool AlreadyApplied = false,
    IReadOnlyCollection<string>? Warnings = null)
{
    public static PerformanceOperationResult Success(
        Guid reportId,
        bool alreadyApplied = false,
        IReadOnlyCollection<string>? warnings = null) =>
        new(true, reportId, null, alreadyApplied, warnings ?? []);

    public static PerformanceOperationResult Failure(string code) => new(false, null, code);
}

public sealed record PerformanceReportView(
    Guid MatchId,
    string MatchStatus,
    string Category,
    Guid? ReportId,
    MatchStatisticsSource? Source,
    MatchStatisticsStatus? Status,
    string? ReplayFileName,
    string? ExtractorVersion,
    bool IsComplete,
    IReadOnlyCollection<PerformancePlayerView> Players,
    IReadOnlyCollection<Guid> ConfirmedTeamIds);

public sealed record PerformancePlayerView(
    Guid PlayerProfileId,
    Guid TeamId,
    string DisplayName,
    TeamResult TeamResult,
    StatisticValueOrigin? Origin,
    MatchStatisticValues Values,
    decimal? PerformanceIndex,
    PerformanceAwardType? AwardType,
    int BonusPoints);
