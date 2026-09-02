namespace AgeNexus.Application.MatchPerformance;

public interface IReplayStatisticsExtractor
{
    Task<ReplayExtractionResult> ExtractAsync(
        string fileName,
        byte[] content,
        CancellationToken cancellationToken = default);
}

public sealed record ReplayExtractionResult(
    bool Succeeded,
    string? ExtractorVersion,
    string CoverageDetails,
    IReadOnlyCollection<ReplayPlayerStatistics> Players,
    string? ErrorCode = null,
    IReadOnlyCollection<string>? Warnings = null);

public sealed record ReplayPlayerStatistics(
    string Name,
    bool IsHuman,
    int TeamNumber,
    AgeNexus.Domain.MatchPerformance.MatchStatisticValues Values);
