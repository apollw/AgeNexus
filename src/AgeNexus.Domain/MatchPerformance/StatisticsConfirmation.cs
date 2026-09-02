using AgeNexus.Domain.Common;

namespace AgeNexus.Domain.MatchPerformance;

public sealed class StatisticsConfirmation
{
    private StatisticsConfirmation()
    {
    }

    public StatisticsConfirmation(
        Guid id,
        Guid reportId,
        Guid teamId,
        Guid confirmedByPlayerProfileId,
        StatisticsConfirmationDecision decision,
        DateTimeOffset decidedAtUtc)
    {
        if (id == Guid.Empty || reportId == Guid.Empty || teamId == Guid.Empty ||
            confirmedByPlayerProfileId == Guid.Empty || decidedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new DomainRuleException("A statistics confirmation requires ids and an UTC timestamp.");
        }

        Id = id;
        ReportId = reportId;
        TeamId = teamId;
        ConfirmedByPlayerProfileId = confirmedByPlayerProfileId;
        Decision = decision;
        DecidedAtUtc = decidedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ReportId { get; private set; }
    public Guid TeamId { get; private set; }
    public Guid ConfirmedByPlayerProfileId { get; private set; }
    public StatisticsConfirmationDecision Decision { get; private set; }
    public DateTimeOffset DecidedAtUtc { get; private set; }
}
