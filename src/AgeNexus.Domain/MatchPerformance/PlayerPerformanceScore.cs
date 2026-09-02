using AgeNexus.Domain.Common;

namespace AgeNexus.Domain.MatchPerformance;

public sealed class PlayerPerformanceScore
{
    private PlayerPerformanceScore()
    {
        FormulaVersion = null!;
    }

    public PlayerPerformanceScore(
        Guid id,
        Guid reportId,
        Guid matchId,
        Guid teamId,
        Guid playerProfileId,
        decimal military,
        decimal economy,
        decimal technology,
        decimal society,
        decimal overall,
        PerformanceAwardType awardType,
        int bonusPoints,
        string formulaVersion,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty || reportId == Guid.Empty || matchId == Guid.Empty || teamId == Guid.Empty ||
            playerProfileId == Guid.Empty || string.IsNullOrWhiteSpace(formulaVersion) ||
            createdAtUtc.Offset != TimeSpan.Zero || bonusPoints is < 0 or > 2 ||
            new[] { military, economy, technology, society, overall }.Any(x => x is < 0m or > 1m))
        {
            throw new DomainRuleException("A performance score contains invalid ids, normalized values or points.");
        }

        Id = id;
        ReportId = reportId;
        MatchId = matchId;
        TeamId = teamId;
        PlayerProfileId = playerProfileId;
        Military = military;
        Economy = economy;
        Technology = technology;
        Society = society;
        Overall = overall;
        AwardType = awardType;
        BonusPoints = bonusPoints;
        FormulaVersion = formulaVersion.Trim();
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ReportId { get; private set; }
    public Guid MatchId { get; private set; }
    public Guid TeamId { get; private set; }
    public Guid PlayerProfileId { get; private set; }
    public decimal Military { get; private set; }
    public decimal Economy { get; private set; }
    public decimal Technology { get; private set; }
    public decimal Society { get; private set; }
    public decimal Overall { get; private set; }
    public PerformanceAwardType AwardType { get; private set; }
    public int BonusPoints { get; private set; }
    public string FormulaVersion { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
}
