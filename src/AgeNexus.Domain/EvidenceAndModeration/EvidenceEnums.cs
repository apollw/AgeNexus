namespace AgeNexus.Domain.EvidenceAndModeration;

public enum EvidenceLevel
{
    None,
    Basic,
    Verified,
    Audited
}

public enum EvidenceKind
{
    ConfigurationScreenshot,
    ResultScreenshot,
    Replay,
    VideoLink,
    Comment
}

public enum VerificationStatus
{
    Draft,
    Submitted,
    PendingReview,
    Basic,
    Verified,
    Audited,
    Contested,
    Rejected,
    Annulled
}
