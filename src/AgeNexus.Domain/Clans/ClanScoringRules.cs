using AgeNexus.Domain.Competition;

namespace AgeNexus.Domain.Clans;

public static class ClanScoringRules
{
    public static decimal GetRepresentationFactor(int clanHumanMembers, int totalHumanMembers)
    {
        if (totalHumanMembers < 1 || clanHumanMembers < 0 || clanHumanMembers > totalHumanMembers)
        {
            throw new ArgumentOutOfRangeException(nameof(clanHumanMembers));
        }

        var factor = (decimal)clanHumanMembers / totalHumanMembers;
        return factor >= 0.50m ? factor : 0m;
    }

    public static int GetPrestigeBase(int largestHumanTeamSize, ScoringResult result) =>
        largestHumanTeamSize switch
        {
            <= 0 => throw new ArgumentOutOfRangeException(nameof(largestHumanTeamSize)),
            1 => result switch { ScoringResult.Victory => 50, ScoringResult.Draw => 25, _ => 10 },
            2 => result switch { ScoringResult.Victory => 100, ScoringResult.Draw => 50, _ => 25 },
            3 => result switch { ScoringResult.Victory => 120, ScoringResult.Draw => 60, _ => 30 },
            _ => result switch { ScoringResult.Victory => 140, ScoringResult.Draw => 70, _ => 35 }
        };

    public static decimal CalculatePrestige(
        int basePoints,
        decimal representationFactor,
        bool hybridPvp,
        decimal challengeMultiplier)
    {
        if (basePoints < 0 || representationFactor is < 0m or > 1m ||
            challengeMultiplier is < 0.60m or > 1.40m)
        {
            throw new ArgumentOutOfRangeException(nameof(basePoints));
        }

        var modalityFactor = hybridPvp ? 0.60m : 1m;
        return Math.Round(
            basePoints * representationFactor * modalityFactor * challengeMultiplier,
            2,
            MidpointRounding.AwayFromZero);
    }

    public static decimal CalculatePvePoints(
        IReadOnlyCollection<decimal> humanPveAwards,
        decimal representationFactor)
    {
        ArgumentNullException.ThrowIfNull(humanPveAwards);
        if (humanPveAwards.Count == 0 || representationFactor is < 0.50m or > 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(representationFactor));
        }

        return Math.Round(
            humanPveAwards.Average() * representationFactor,
            2,
            MidpointRounding.AwayFromZero);
    }
}
