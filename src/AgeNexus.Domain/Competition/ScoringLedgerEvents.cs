using AgeNexus.Domain.Common;

namespace AgeNexus.Domain.Competition;

public enum RatingScopeKind
{
    GeneralCompetitive,
    OneVersusOne,
    BalancedTeams,
    AsymmetricTeams,
    TeamLineup,
    ClanCompetitive
}

public enum PointScopeKind
{
    Career,
    Pve,
    ClanPrestige,
    ClanPve
}

public sealed class RatingEvent
{
    private RatingEvent()
    {
        RuleVersion = null!;
        CalculationDetails = null!;
    }

    public RatingEvent(
        Guid id,
        Guid matchId,
        Guid beneficiaryId,
        Guid? seasonId,
        RatingScopeKind scope,
        decimal delta,
        string ruleVersion,
        string calculationDetails,
        DateTimeOffset createdAtUtc)
    {
        Validate(id, matchId, beneficiaryId, ruleVersion, calculationDetails, createdAtUtc);
        Id = id;
        MatchId = matchId;
        BeneficiaryId = beneficiaryId;
        SeasonId = seasonId;
        Scope = scope;
        Delta = delta;
        RuleVersion = ruleVersion.Trim();
        CalculationDetails = calculationDetails.Trim();
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid MatchId { get; private set; }
    public Guid BeneficiaryId { get; private set; }
    public Guid? SeasonId { get; private set; }
    public RatingScopeKind Scope { get; private set; }
    public decimal Delta { get; private set; }
    public string RuleVersion { get; private set; }
    public string CalculationDetails { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private static void Validate(
        Guid id,
        Guid matchId,
        Guid beneficiaryId,
        string ruleVersion,
        string details,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty || matchId == Guid.Empty || beneficiaryId == Guid.Empty ||
            string.IsNullOrWhiteSpace(ruleVersion) || string.IsNullOrWhiteSpace(details) ||
            createdAtUtc.Offset != TimeSpan.Zero)
        {
            throw new DomainRuleException("A scoring event requires ids, rule version, details and UTC creation time.");
        }
    }
}

public sealed class PointEvent
{
    private PointEvent()
    {
        RuleVersion = null!;
        CalculationDetails = null!;
    }

    public PointEvent(
        Guid id,
        Guid matchId,
        Guid beneficiaryId,
        Guid? seasonId,
        PointScopeKind scope,
        decimal points,
        string ruleVersion,
        string calculationDetails,
        DateTimeOffset createdAtUtc)
    {
        if (points < 0m)
        {
            throw new DomainRuleException("Point awards cannot be negative; reversals use a linked compensating event.");
        }

        if (id == Guid.Empty || matchId == Guid.Empty || beneficiaryId == Guid.Empty ||
            string.IsNullOrWhiteSpace(ruleVersion) || string.IsNullOrWhiteSpace(calculationDetails) ||
            createdAtUtc.Offset != TimeSpan.Zero)
        {
            throw new DomainRuleException("A point event requires ids, rule version, details and UTC creation time.");
        }

        Id = id;
        MatchId = matchId;
        BeneficiaryId = beneficiaryId;
        SeasonId = seasonId;
        Scope = scope;
        Points = points;
        RuleVersion = ruleVersion.Trim();
        CalculationDetails = calculationDetails.Trim();
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid MatchId { get; private set; }
    public Guid BeneficiaryId { get; private set; }
    public Guid? SeasonId { get; private set; }
    public PointScopeKind Scope { get; private set; }
    public decimal Points { get; private set; }
    public string RuleVersion { get; private set; }
    public string CalculationDetails { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
}
