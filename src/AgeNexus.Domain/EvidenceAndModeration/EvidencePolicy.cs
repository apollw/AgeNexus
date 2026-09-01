namespace AgeNexus.Domain.EvidenceAndModeration;

public static class EvidencePolicy
{
    public static decimal GetPvePointFactor(EvidenceLevel level) => level switch
    {
        EvidenceLevel.None => 0m,
        EvidenceLevel.Basic => 0.40m,
        EvidenceLevel.Verified => 1m,
        EvidenceLevel.Audited => 1m,
        _ => throw new ArgumentOutOfRangeException(nameof(level))
    };

    public static bool IsOfficialPveRankingEligible(EvidenceLevel level) =>
        level is EvidenceLevel.Verified or EvidenceLevel.Audited;

    public static bool IsClanPveEligible(EvidenceLevel level) =>
        level is EvidenceLevel.Verified or EvidenceLevel.Audited;

    public static bool IsRecordEligible(EvidenceLevel level) => level == EvidenceLevel.Audited;
}
