namespace AgeNexus.Application.MatchPerformance;

public sealed record AgeExtractorImage(
    string Category,
    byte[] Content);

public sealed record AgeExtractorExecutionResult(
    bool Succeeded,
    string? Json = null,
    string? ErrorCode = null)
{
    public static AgeExtractorExecutionResult Success(string json) => new(true, json);
    public static AgeExtractorExecutionResult Failure(string errorCode) => new(false, ErrorCode: errorCode);
}

public interface IAgeExtractorService
{
    Task<AgeExtractorExecutionResult> ExtractAsync(
        int playerCount,
        IReadOnlyCollection<AgeExtractorImage> images,
        CancellationToken cancellationToken = default);
}
