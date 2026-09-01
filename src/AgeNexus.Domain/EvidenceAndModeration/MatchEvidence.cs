using AgeNexus.Domain.Common;

namespace AgeNexus.Domain.EvidenceAndModeration;

public sealed class MatchEvidence
{
    private MatchEvidence()
    {
    }

    public MatchEvidence(
        Guid id,
        Guid matchId,
        Guid submittedByPlayerProfileId,
        EvidenceKind kind,
        DateTimeOffset submittedAtUtc,
        string? objectKey = null,
        string? externalUrl = null,
        string? sha256 = null)
    {
        if (id == Guid.Empty || matchId == Guid.Empty || submittedByPlayerProfileId == Guid.Empty)
        {
            throw new DomainRuleException("Evidence, match and submitter ids are required.");
        }

        if (submittedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new DomainRuleException("Evidence submission time must be provided in UTC.");
        }

        var normalizedObjectKey = NormalizeOptional(objectKey);
        var normalizedUrl = NormalizeOptional(externalUrl);
        if (normalizedObjectKey is null && normalizedUrl is null && kind != EvidenceKind.Comment)
        {
            throw new DomainRuleException("Evidence requires an object key or external URL.");
        }

        if (normalizedUrl is not null &&
            (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new DomainRuleException("External evidence URL must be an absolute HTTPS URL.");
        }

        var normalizedHash = NormalizeOptional(sha256)?.ToLowerInvariant();
        if (normalizedHash is not null &&
            (normalizedHash.Length != 64 || normalizedHash.Any(x => !Uri.IsHexDigit(x))))
        {
            throw new DomainRuleException("Replay fingerprint must be a SHA-256 hexadecimal value.");
        }

        if (kind == EvidenceKind.Replay && normalizedHash is null)
        {
            throw new DomainRuleException("Replay evidence requires a SHA-256 fingerprint.");
        }

        Id = id;
        MatchId = matchId;
        SubmittedByPlayerProfileId = submittedByPlayerProfileId;
        Kind = kind;
        SubmittedAtUtc = submittedAtUtc;
        ObjectKey = normalizedObjectKey;
        ExternalUrl = normalizedUrl;
        Sha256 = normalizedHash;
    }

    public Guid Id { get; private set; }
    public Guid MatchId { get; private set; }
    public Guid SubmittedByPlayerProfileId { get; private set; }
    public EvidenceKind Kind { get; private set; }
    public DateTimeOffset SubmittedAtUtc { get; private set; }
    public string? ObjectKey { get; private set; }
    public string? ExternalUrl { get; private set; }
    public string? Sha256 { get; private set; }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
