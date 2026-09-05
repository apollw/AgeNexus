using System.Security.Cryptography;
using AgeNexus.Application.Evidence;
using AgeNexus.Domain.EvidenceAndModeration;
using AgeNexus.Domain.Matches;
using AgeNexus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AgeNexus.Infrastructure.Evidence;

internal sealed class MatchEvidenceService(
    AgeNexusDbContext database,
    IEvidenceObjectStorage storage,
    IConfiguration configuration,
    ILogger<MatchEvidenceService> logger) : IMatchEvidenceService
{
    private const int MaximumScreenshots = 5;
    private const int MaximumScreenshotBytes = 4 * 1024 * 1024;

    public async Task<MatchEvidenceGallery?> GetAsync(
        Guid matchId,
        CancellationToken cancellationToken = default)
    {
        var playedAtUtc = await database.Matches.AsNoTracking()
            .Where(match => match.Id == matchId)
            .Select(match => (DateTimeOffset?)match.PlayedAtUtc)
            .SingleOrDefaultAsync(cancellationToken);
        if (!playedAtUtc.HasValue)
        {
            return null;
        }

        var evidence = await database.MatchEvidence.AsNoTracking()
            .Where(item => item.MatchId == matchId &&
                           (item.Kind == EvidenceKind.VideoLink || item.Kind == EvidenceKind.ResultScreenshot))
            .OrderBy(item => item.SubmittedAtUtc)
            .ToArrayAsync(cancellationToken);
        var items = evidence.Select(item =>
        {
            if (item.Kind == EvidenceKind.VideoLink &&
                YouTubeVideoUrl.TryParse(item.ExternalUrl, out var video) && video is not null)
            {
                return new MatchEvidenceItem(item.Id, item.Kind, video.WatchUrl, video.EmbedUrl, item.SubmittedAtUtc);
            }

            var url = item.ObjectKey is not null && storage.IsConfigured
                ? storage.GetPublicUrl(item.ObjectKey)
                : item.ExternalUrl ?? string.Empty;
            return new MatchEvidenceItem(item.Id, item.Kind, url, null, item.SubmittedAtUtc);
        }).Where(item => !string.IsNullOrWhiteSpace(item.Url)).ToArray();

        return new MatchEvidenceGallery(matchId, playedAtUtc.Value, items);
    }

    public async Task<MatchEvidenceOperationResult> AddYouTubeVideoAsync(
        Guid matchId,
        Guid submittedByPlayerProfileId,
        string url,
        CancellationToken cancellationToken = default)
    {
        var match = await LoadMatchAsync(matchId, cancellationToken);
        if (match is null)
        {
            return MatchEvidenceOperationResult.Failure("MatchNotFound");
        }

        if (!CanManageMatch(match, submittedByPlayerProfileId))
        {
            return MatchEvidenceOperationResult.Failure("EvidenceNotAuthorized");
        }

        if (!YouTubeVideoUrl.TryParse(url, out var video) || video is null)
        {
            return MatchEvidenceOperationResult.Failure("InvalidYouTubeUrl");
        }

        var existing = await database.MatchEvidence
            .SingleOrDefaultAsync(item => item.MatchId == matchId && item.Kind == EvidenceKind.VideoLink,
                cancellationToken);
        if (existing?.ExternalUrl == video.WatchUrl)
        {
            return MatchEvidenceOperationResult.Success(existing.Id);
        }

        if (existing is not null)
        {
            database.MatchEvidence.Remove(existing);
        }

        var evidence = new MatchEvidence(
            Guid.NewGuid(), matchId, submittedByPlayerProfileId, EvidenceKind.VideoLink,
            DateTimeOffset.UtcNow, externalUrl: video.WatchUrl);
        database.MatchEvidence.Add(evidence);
        await database.SaveChangesAsync(cancellationToken);
        return MatchEvidenceOperationResult.Success(evidence.Id);
    }

    public async Task<MatchEvidenceOperationResult> AddScreenshotAsync(
        Guid matchId,
        Guid submittedByPlayerProfileId,
        string fileName,
        string contentType,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        var match = await LoadMatchAsync(matchId, cancellationToken);
        if (match is null)
        {
            return MatchEvidenceOperationResult.Failure("MatchNotFound");
        }

        if (!CanManageMatch(match, submittedByPlayerProfileId))
        {
            return MatchEvidenceOperationResult.Failure("EvidenceNotAuthorized");
        }

        if (!storage.IsConfigured)
        {
            return MatchEvidenceOperationResult.Failure("EvidenceStorageNotConfigured");
        }

        if (content.Length == 0 || content.Length > MaximumScreenshotBytes ||
            !ScreenshotFile.TryIdentify(fileName, content, out var image))
        {
            return MatchEvidenceOperationResult.Failure("InvalidScreenshot");
        }

        var existingCount = await database.MatchEvidence.AsNoTracking()
            .CountAsync(item => item.MatchId == matchId && item.Kind == EvidenceKind.ResultScreenshot,
                cancellationToken);
        if (existingCount >= MaximumScreenshots)
        {
            return MatchEvidenceOperationResult.Failure("ScreenshotLimitReached");
        }

        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        if (await database.MatchEvidence.AsNoTracking().AnyAsync(item => item.Sha256 == hash, cancellationToken))
        {
            return MatchEvidenceOperationResult.Failure("DuplicateScreenshot");
        }

        var evidenceId = Guid.NewGuid();
        var objectKey = $"{matchId:N}/{evidenceId:N}.{image.Extension}";
        try
        {
            await storage.UploadAsync(objectKey, image.ContentType, content, cancellationToken);
            database.MatchEvidence.Add(new MatchEvidence(
                evidenceId, matchId, submittedByPlayerProfileId, EvidenceKind.ResultScreenshot,
                DateTimeOffset.UtcNow, objectKey: objectKey, sha256: hash));
            await database.SaveChangesAsync(cancellationToken);
            return MatchEvidenceOperationResult.Success(evidenceId);
        }
        catch (Exception exception) when (exception is HttpRequestException or DbUpdateException)
        {
            logger.LogError(exception, "Falha ao armazenar captura da partida {MatchId}.", matchId);
            try
            {
                await storage.DeleteAsync(objectKey, CancellationToken.None);
            }
            catch (HttpRequestException cleanupException)
            {
                logger.LogWarning(cleanupException, "Falha ao remover objeto órfão {ObjectKey}.", objectKey);
            }

            return MatchEvidenceOperationResult.Failure("ScreenshotUploadFailed");
        }
    }

    public async Task<MatchEvidenceOperationResult> DeleteAsync(
        Guid evidenceId,
        Guid requestedByPlayerProfileId,
        CancellationToken cancellationToken = default)
    {
        var evidence = await database.MatchEvidence.SingleOrDefaultAsync(item => item.Id == evidenceId, cancellationToken);
        if (evidence is null)
        {
            return MatchEvidenceOperationResult.Failure("EvidenceNotFound");
        }

        var match = await LoadMatchAsync(evidence.MatchId, cancellationToken);
        if (match is null || !CanManageMatch(match, requestedByPlayerProfileId))
        {
            return MatchEvidenceOperationResult.Failure("EvidenceNotAuthorized");
        }

        var objectKey = evidence.ObjectKey;
        database.MatchEvidence.Remove(evidence);
        await database.SaveChangesAsync(cancellationToken);
        if (objectKey is not null && storage.IsConfigured)
        {
            try
            {
                await storage.DeleteAsync(objectKey, cancellationToken);
            }
            catch (HttpRequestException exception)
            {
                logger.LogWarning(exception, "A evidência {EvidenceId} foi removida, mas o objeto {ObjectKey} permaneceu no storage.",
                    evidenceId, objectKey);
            }
        }

        return MatchEvidenceOperationResult.Success(evidenceId);
    }

    private Task<Match?> LoadMatchAsync(Guid matchId, CancellationToken cancellationToken) =>
        database.Matches.Include(match => match.Teams).ThenInclude(team => team.Participants)
            .SingleOrDefaultAsync(match => match.Id == matchId, cancellationToken);

    private bool CanManageMatch(Match match, Guid playerId) =>
        match.Teams.SelectMany(team => team.Participants)
            .Any(participant => participant.Type == ParticipantType.Human && participant.PlayerProfileId == playerId) ||
        (configuration.GetValue("OperatingMode:SingleAdministrator", true) &&
         match.CreatedByPlayerProfileId == playerId);

    private sealed record ScreenshotFile(string Extension, string ContentType)
    {
        public static bool TryIdentify(
            string fileName,
            byte[] content,
            out ScreenshotFile image)
        {
            image = null!;
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            if (content.Length >= 8 && extension == ".png" &&
                content.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }))
            {
                image = new ScreenshotFile("png", "image/png");
                return true;
            }

            if (content.Length >= 3 && extension is ".jpg" or ".jpeg" &&
                content[0] == 0xff && content[1] == 0xd8 && content[2] == 0xff)
            {
                image = new ScreenshotFile("jpg", "image/jpeg");
                return true;
            }

            if (content.Length >= 12 && extension == ".webp" &&
                content.AsSpan(0, 4).SequenceEqual("RIFF"u8) && content.AsSpan(8, 4).SequenceEqual("WEBP"u8))
            {
                image = new ScreenshotFile("webp", "image/webp");
                return true;
            }

            return false;
        }
    }
}
