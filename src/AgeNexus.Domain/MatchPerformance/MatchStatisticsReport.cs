using System.Text.Json;
using System.Text.RegularExpressions;
using AgeNexus.Domain.Common;

namespace AgeNexus.Domain.MatchPerformance;

public sealed partial class MatchStatisticsReport
{
    private MatchStatisticsReport()
    {
        CoverageDetails = "{}";
    }

    public MatchStatisticsReport(
        Guid id,
        Guid matchId,
        Guid submittedByPlayerProfileId,
        MatchStatisticsSource source,
        DateTimeOffset createdAtUtc,
        string? replayFileName = null,
        string? replaySha256 = null,
        string? extractorVersion = null,
        string? coverageDetails = null)
    {
        if (id == Guid.Empty || matchId == Guid.Empty || submittedByPlayerProfileId == Guid.Empty ||
            createdAtUtc.Offset != TimeSpan.Zero)
        {
            throw new DomainRuleException("A statistics report requires ids and an UTC creation time.");
        }

        ValidateReplay(source, replayFileName, replaySha256);
        ValidateJson(coverageDetails);
        Id = id;
        MatchId = matchId;
        SubmittedByPlayerProfileId = submittedByPlayerProfileId;
        Source = source;
        CreatedAtUtc = createdAtUtc;
        ReplayFileName = Normalize(replayFileName, 260);
        ReplaySha256 = Normalize(replaySha256, 64)?.ToLowerInvariant();
        ExtractorVersion = Normalize(extractorVersion, 100);
        CoverageDetails = string.IsNullOrWhiteSpace(coverageDetails) ? "{}" : coverageDetails.Trim();
    }

    public Guid Id { get; private set; }
    public Guid MatchId { get; private set; }
    public Guid SubmittedByPlayerProfileId { get; private set; }
    public MatchStatisticsSource Source { get; private set; }
    public MatchStatisticsStatus Status { get; private set; } = MatchStatisticsStatus.Draft;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? SubmittedAtUtc { get; private set; }
    public DateTimeOffset? ConfirmedAtUtc { get; private set; }
    public DateTimeOffset? AwardedAtUtc { get; private set; }
    public string? ReplayFileName { get; private set; }
    public string? ReplaySha256 { get; private set; }
    public string? ExtractorVersion { get; private set; }
    public string CoverageDetails { get; private set; }

    public void MarkManualCompletion()
    {
        EnsureDraft();
        if (Source == MatchStatisticsSource.Replay)
        {
            Source = MatchStatisticsSource.ReplayWithManualCompletion;
        }
    }

    public void Submit(DateTimeOffset submittedAtUtc, bool isComplete)
    {
        EnsureDraft();
        if (!isComplete || submittedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new DomainRuleException("Only a complete report can be submitted with an UTC timestamp.");
        }

        Status = MatchStatisticsStatus.Submitted;
        SubmittedAtUtc = submittedAtUtc;
    }

    public void MarkConfirmed(DateTimeOffset confirmedAtUtc)
    {
        if (Status != MatchStatisticsStatus.Submitted || confirmedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new DomainRuleException("Only a submitted report can be confirmed.");
        }

        Status = MatchStatisticsStatus.Confirmed;
        ConfirmedAtUtc = confirmedAtUtc;
    }

    public void MarkAwarded(DateTimeOffset awardedAtUtc)
    {
        if (Status != MatchStatisticsStatus.Confirmed || awardedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new DomainRuleException("Only a confirmed report can generate performance awards.");
        }

        Status = MatchStatisticsStatus.Awarded;
        AwardedAtUtc = awardedAtUtc;
    }

    public void Reject()
    {
        if (Status == MatchStatisticsStatus.Awarded)
        {
            throw new DomainRuleException("An awarded report must be reversed through the scoring ledger.");
        }

        Status = MatchStatisticsStatus.Rejected;
    }

    private void EnsureDraft()
    {
        if (Status != MatchStatisticsStatus.Draft)
        {
            throw new DomainRuleException("Statistics can only be edited while the report is a draft.");
        }
    }

    private static void ValidateReplay(MatchStatisticsSource source, string? fileName, string? sha256)
    {
        var replaySource = source is MatchStatisticsSource.Replay or MatchStatisticsSource.ReplayWithManualCompletion;
        if (!replaySource)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(fileName) || !HasSupportedReplayExtension(fileName) ||
            string.IsNullOrWhiteSpace(sha256) || !Sha256Regex().IsMatch(sha256))
        {
            throw new DomainRuleException("A replay report requires a supported Age II replay and a SHA-256 fingerprint.");
        }
    }

    private static bool HasSupportedReplayExtension(string fileName) =>
        fileName.EndsWith(".aoe2record", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".mgz", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".mgx", StringComparison.OrdinalIgnoreCase);

    private static void ValidateJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        try
        {
            JsonDocument.Parse(value);
        }
        catch (JsonException exception)
        {
            throw new DomainRuleException($"Coverage details must be valid JSON: {exception.Message}");
        }
    }

    private static string? Normalize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new DomainRuleException($"Value cannot exceed {maxLength} characters.");
        }

        return normalized;
    }

    [GeneratedRegex("^[a-fA-F0-9]{64}$")]
    private static partial Regex Sha256Regex();
}
