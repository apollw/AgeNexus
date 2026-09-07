using AgeNexus.Domain.EvidenceAndModeration;

namespace AgeNexus.Application.Evidence;

public sealed record MatchEvidenceItem(
    Guid EvidenceId,
    EvidenceKind Kind,
    string Url,
    string? EmbedUrl,
    DateTimeOffset SubmittedAtUtc,
    string? FileName = null);

public sealed record MatchEvidenceGallery(
    Guid MatchId,
    DateTimeOffset PlayedAtUtc,
    IReadOnlyCollection<MatchEvidenceItem> Items);

public sealed record MatchEvidenceOperationResult(
    bool Succeeded,
    string? ErrorCode = null,
    Guid? EvidenceId = null)
{
    public static MatchEvidenceOperationResult Success(Guid evidenceId) =>
        new(true, EvidenceId: evidenceId);

    public static MatchEvidenceOperationResult Failure(string errorCode) =>
        new(false, errorCode);
}

public interface IMatchEvidenceService
{
    Task<MatchEvidenceGallery?> GetAsync(
        Guid matchId,
        CancellationToken cancellationToken = default);

    Task<MatchEvidenceOperationResult> AddYouTubeVideoAsync(
        Guid matchId,
        Guid submittedByPlayerProfileId,
        string url,
        CancellationToken cancellationToken = default);

    Task<MatchEvidenceOperationResult> AddScreenshotAsync(
        Guid matchId,
        Guid submittedByPlayerProfileId,
        string fileName,
        string contentType,
        byte[] content,
        CancellationToken cancellationToken = default);

    Task<MatchEvidenceOperationResult> AddReplayAsync(
        Guid matchId,
        Guid submittedByPlayerProfileId,
        string fileName,
        byte[] content,
        CancellationToken cancellationToken = default);

    Task<MatchEvidenceOperationResult> DeleteAsync(
        Guid evidenceId,
        Guid requestedByPlayerProfileId,
        CancellationToken cancellationToken = default);
}

public interface IEvidenceObjectStorage
{
    bool IsConfigured { get; }

    Task UploadAsync(
        string objectKey,
        string contentType,
        byte[] content,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string objectKey,
        CancellationToken cancellationToken = default);

    string GetPublicUrl(string objectKey);

    string GetPublicDownloadUrl(string objectKey, string fileName);
}
