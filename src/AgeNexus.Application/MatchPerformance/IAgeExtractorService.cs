namespace AgeNexus.Application.MatchPerformance;

public sealed record AgeExtractorPoint(
    double X,
    double Y);

public sealed record AgeExtractorImage(
    string Category,
    byte[] Content,
    IReadOnlyCollection<AgeExtractorPoint> TableCorners);

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
