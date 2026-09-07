namespace AgeNexus.Domain.MatchPerformance;

public enum MatchStatisticsSource
{
    Replay,
    ReplayWithManualCompletion,
    ScreenshotTranscription,
    Manual,
    JsonImport
}

public enum MatchStatisticsStatus
{
    Draft,
    Submitted,
    Confirmed,
    Awarded,
    Rejected
}

public enum StatisticValueOrigin
{
    Extracted,
    Calculated,
    Estimated,
    Manual,
    Screenshot,
    JsonImport
}

public enum StatisticsConfirmationDecision
{
    Confirmed,
    Contested
}

public enum PerformanceAwardType
{
    None,
    Mvp,
    SharedMvp,
    DefeatedTeamHighlight
}
