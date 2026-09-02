using AgeNexus.Domain.Common;

namespace AgeNexus.Domain.MatchPerformance;

public sealed record MatchStatisticValues(
    int? UnitsKilled = null,
    int? UnitsLost = null,
    int? BuildingsDestroyed = null,
    int? BuildingsLost = null,
    int? LargestArmy = null,
    int? PeakVillagers = null,
    long? FoodCollected = null,
    long? WoodCollected = null,
    long? GoldCollected = null,
    long? StoneCollected = null,
    int? MilitaryScore = null,
    int? EconomyScore = null,
    int? TechnologyScore = null,
    int? SocietyScore = null,
    int? TotalScore = null,
    int? UnitsConverted = null,
    long? TradeGold = null,
    long? RelicGold = null,
    long? TributeSent = null,
    long? TributeReceived = null,
    int? ResearchCount = null,
    decimal? ExploredPercent = null,
    int? FeudalAgeSeconds = null,
    int? CastleAgeSeconds = null,
    int? ImperialAgeSeconds = null,
    int? EffectiveActionsPerMinute = null);

public sealed class PlayerMatchStatistics
{
    private PlayerMatchStatistics()
    {
    }

    public PlayerMatchStatistics(
        Guid id,
        Guid reportId,
        Guid matchId,
        Guid teamId,
        Guid playerProfileId,
        StatisticValueOrigin origin,
        MatchStatisticValues values)
    {
        if (id == Guid.Empty || reportId == Guid.Empty || matchId == Guid.Empty ||
            teamId == Guid.Empty || playerProfileId == Guid.Empty)
        {
            throw new DomainRuleException("Player statistics require report, match, team and player ids.");
        }

        Id = id;
        ReportId = reportId;
        MatchId = matchId;
        TeamId = teamId;
        PlayerProfileId = playerProfileId;
        Apply(values, origin);
    }

    public Guid Id { get; private set; }
    public Guid ReportId { get; private set; }
    public Guid MatchId { get; private set; }
    public Guid TeamId { get; private set; }
    public Guid PlayerProfileId { get; private set; }
    public StatisticValueOrigin Origin { get; private set; }
    public int? UnitsKilled { get; private set; }
    public int? UnitsLost { get; private set; }
    public int? BuildingsDestroyed { get; private set; }
    public int? BuildingsLost { get; private set; }
    public int? LargestArmy { get; private set; }
    public int? PeakVillagers { get; private set; }
    public long? FoodCollected { get; private set; }
    public long? WoodCollected { get; private set; }
    public long? GoldCollected { get; private set; }
    public long? StoneCollected { get; private set; }
    public int? MilitaryScore { get; private set; }
    public int? EconomyScore { get; private set; }
    public int? TechnologyScore { get; private set; }
    public int? SocietyScore { get; private set; }
    public int? TotalScore { get; private set; }
    public int? UnitsConverted { get; private set; }
    public long? TradeGold { get; private set; }
    public long? RelicGold { get; private set; }
    public long? TributeSent { get; private set; }
    public long? TributeReceived { get; private set; }
    public int? ResearchCount { get; private set; }
    public decimal? ExploredPercent { get; private set; }
    public int? FeudalAgeSeconds { get; private set; }
    public int? CastleAgeSeconds { get; private set; }
    public int? ImperialAgeSeconds { get; private set; }
    public int? EffectiveActionsPerMinute { get; private set; }

    public bool IsComplete =>
        UnitsKilled.HasValue && UnitsLost.HasValue && BuildingsDestroyed.HasValue && BuildingsLost.HasValue &&
        LargestArmy.HasValue && PeakVillagers.HasValue && FoodCollected.HasValue && WoodCollected.HasValue &&
        GoldCollected.HasValue && StoneCollected.HasValue && MilitaryScore.HasValue && EconomyScore.HasValue &&
        TechnologyScore.HasValue && SocietyScore.HasValue && TotalScore.HasValue;

    public void Apply(MatchStatisticValues values, StatisticValueOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(values);
        Validate(values);
        Origin = origin;
        UnitsKilled = values.UnitsKilled;
        UnitsLost = values.UnitsLost;
        BuildingsDestroyed = values.BuildingsDestroyed;
        BuildingsLost = values.BuildingsLost;
        LargestArmy = values.LargestArmy;
        PeakVillagers = values.PeakVillagers;
        FoodCollected = values.FoodCollected;
        WoodCollected = values.WoodCollected;
        GoldCollected = values.GoldCollected;
        StoneCollected = values.StoneCollected;
        MilitaryScore = values.MilitaryScore;
        EconomyScore = values.EconomyScore;
        TechnologyScore = values.TechnologyScore;
        SocietyScore = values.SocietyScore;
        TotalScore = values.TotalScore;
        UnitsConverted = values.UnitsConverted;
        TradeGold = values.TradeGold;
        RelicGold = values.RelicGold;
        TributeSent = values.TributeSent;
        TributeReceived = values.TributeReceived;
        ResearchCount = values.ResearchCount;
        ExploredPercent = values.ExploredPercent;
        FeudalAgeSeconds = values.FeudalAgeSeconds;
        CastleAgeSeconds = values.CastleAgeSeconds;
        ImperialAgeSeconds = values.ImperialAgeSeconds;
        EffectiveActionsPerMinute = values.EffectiveActionsPerMinute;
    }

    public MatchStatisticValues ToValues() => new(
        UnitsKilled, UnitsLost, BuildingsDestroyed, BuildingsLost, LargestArmy, PeakVillagers,
        FoodCollected, WoodCollected, GoldCollected, StoneCollected, MilitaryScore, EconomyScore,
        TechnologyScore, SocietyScore, TotalScore, UnitsConverted, TradeGold, RelicGold, TributeSent,
        TributeReceived, ResearchCount, ExploredPercent, FeudalAgeSeconds, CastleAgeSeconds,
        ImperialAgeSeconds, EffectiveActionsPerMinute);

    private static void Validate(MatchStatisticValues values)
    {
        var integers = new int?[]
        {
            values.UnitsKilled, values.UnitsLost, values.BuildingsDestroyed, values.BuildingsLost,
            values.LargestArmy, values.PeakVillagers, values.MilitaryScore, values.EconomyScore,
            values.TechnologyScore, values.SocietyScore, values.TotalScore, values.UnitsConverted,
            values.ResearchCount, values.FeudalAgeSeconds, values.CastleAgeSeconds,
            values.ImperialAgeSeconds, values.EffectiveActionsPerMinute
        };
        var longs = new long?[]
        {
            values.FoodCollected, values.WoodCollected, values.GoldCollected, values.StoneCollected,
            values.TradeGold, values.RelicGold, values.TributeSent, values.TributeReceived
        };
        if (integers.Any(x => x < 0) || longs.Any(x => x < 0) ||
            values.ExploredPercent is < 0m or > 100m)
        {
            throw new DomainRuleException("Match statistics cannot contain negative values or invalid percentages.");
        }
    }
}
