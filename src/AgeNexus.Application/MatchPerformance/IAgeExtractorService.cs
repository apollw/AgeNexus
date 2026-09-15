namespace AgeNexus.Application.MatchPerformance;

public sealed record AgeExtractorPoint(
    double X,
    double Y);

public sealed record AgeExtractorImage(
    string Category,
    byte[] Content,
    IReadOnlyCollection<AgeExtractorPoint> TableCorners);

public sealed record AgeExtractorProgress(
    int Completed,
    int Total,
    decimal Percentage,
    string Message);

public sealed record AgeExtractorExecutionResult(
    bool Succeeded,
    string? Json = null,
    string? ErrorCode = null,
    string? ErrorMessage = null)
{
    public static AgeExtractorExecutionResult Success(string json) => new(true, json);
    public static AgeExtractorExecutionResult Failure(string errorCode, string? errorMessage = null) =>
        new(false, ErrorCode: errorCode, ErrorMessage: errorMessage);
}

public interface IAgeExtractorService
{
    Task<AgeExtractorExecutionResult> ExtractAsync(
        int playerCount,
        IReadOnlyCollection<AgeExtractorImage> images,
        IProgress<AgeExtractorProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
