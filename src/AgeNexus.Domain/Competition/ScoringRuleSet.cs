namespace AgeNexus.Domain.Competition;

public enum ScoringResult
{
    Defeat,
    Draw,
    Victory
}

public sealed record AiScoringRule(
    int InternalLevel,
    int VictoryPoints,
    int ValidDefeatPoints,
    decimal EquivalentRating);

public sealed class ScoringRuleSet
{
    private static readonly IReadOnlyDictionary<int, AiScoringRule> AiRules =
        new Dictionary<int, AiScoringRule>
        {
            [1] = new(1, 8, 1, 600m),
            [2] = new(2, 15, 2, 800m),
            [3] = new(3, 30, 4, 1000m),
            [4] = new(4, 50, 6, 1200m),
            [5] = new(5, 75, 8, 1400m)
        };

    public const string CurrentVersion = "2026.09";
    public const decimal InitialRating = 1000m;
    public const decimal PveCareerContribution = 0.35m;
    public const decimal HybridCareerFactor = 0.70m;
    public const int BasicEvidenceSeasonCap = 150;

    public string Version => CurrentVersion;

    public AiScoringRule GetAiRule(int internalLevel) =>
        AiRules.TryGetValue(internalLevel, out var rule)
            ? rule
            : throw new ArgumentOutOfRangeException(nameof(internalLevel), "AI level must be between 1 and 5.");

    public int GetPvpCareerBase(int largestHumanTeamSize, ScoringResult result)
    {
        if (largestHumanTeamSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(largestHumanTeamSize));
        }

        return largestHumanTeamSize switch
        {
            1 => result switch { ScoringResult.Victory => 100, ScoringResult.Draw => 50, _ => 25 },
            2 => result switch { ScoringResult.Victory => 75, ScoringResult.Draw => 38, _ => 20 },
            3 => result switch { ScoringResult.Victory => 60, ScoringResult.Draw => 30, _ => 18 },
            _ => result switch { ScoringResult.Victory => 50, ScoringResult.Draw => 25, _ => 15 }
        };
    }

    public decimal GetAsymmetricCareerMultiplier(
        int ownHumanTeamSize,
        int opponentHumanTeamSize,
        ScoringResult result)
    {
        if (ownHumanTeamSize < 1 || opponentHumanTeamSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(ownHumanTeamSize), "Team sizes must be positive.");
        }

        var difference = Math.Abs(ownHumanTeamSize - opponentHumanTeamSize);
        if (difference == 0 || result == ScoringResult.Draw)
        {
            return 1m;
        }

        var isSmallerTeam = ownHumanTeamSize < opponentHumanTeamSize;
        if (result == ScoringResult.Victory)
        {
            return isSmallerTeam
                ? Math.Min(1.75m, 1m + (0.25m * difference))
                : Math.Max(0.50m, 1m - (0.15m * difference));
        }

        return isSmallerTeam ? 1m : 0.75m;
    }

    public decimal GetModalityWeight(int largestHumanTeamSize, bool asymmetric) =>
        asymmetric
            ? 0.75m
            : largestHumanTeamSize switch
            {
                <= 1 => 1m,
                2 => 0.90m,
                3 => 0.85m,
                _ => 0.80m
            };

    public decimal CalculateEffectiveTeamStrength(IReadOnlyCollection<decimal> participantRatings)
    {
        ArgumentNullException.ThrowIfNull(participantRatings);
        if (participantRatings.Count == 0)
        {
            throw new ArgumentException("At least one participant rating is required.", nameof(participantRatings));
        }

        var average = participantRatings.Average();
        var numericalAdvantage = 240m * (decimal)Math.Log2(participantRatings.Count);
        return average + numericalAdvantage;
    }

    public decimal CalculateExpectedScore(decimal ownStrength, decimal opponentStrength)
    {
        var exponent = (double)((opponentStrength - ownStrength) / 400m);
        return (decimal)(1d / (1d + Math.Pow(10d, exponent)));
    }

    public decimal CalculateRatingDelta(
        decimal expectedScore,
        ScoringResult result,
        int matchesPlayed,
        decimal modalityWeight,
        decimal categoryFactor = 1m)
    {
        ValidateUnitInterval(expectedScore, nameof(expectedScore));
        ValidateUnitInterval(modalityWeight, nameof(modalityWeight));
        ValidateUnitInterval(categoryFactor, nameof(categoryFactor));

        var actualScore = result switch
        {
            ScoringResult.Victory => 1m,
            ScoringResult.Draw => 0.5m,
            _ => 0m
        };
        var k = matchesPlayed < 10 ? 40m : 24m;
        var delta = k * modalityWeight * categoryFactor * (actualScore - expectedScore);
        return Math.Round(Math.Clamp(delta, -40m, 40m), 0, MidpointRounding.AwayFromZero);
    }

    public decimal GetHybridRatingFactor(int aiParticipants, int totalParticipants)
    {
        if (totalParticipants < 1 || aiParticipants < 0 || aiParticipants > totalParticipants)
        {
            throw new ArgumentOutOfRangeException(nameof(aiParticipants));
        }

        var aiProportion = (decimal)aiParticipants / totalParticipants;
        return Math.Clamp(1m - (0.50m * aiProportion), 0.50m, 0.85m);
    }

    public decimal GetChallengeMultiplier(decimal expectedScore)
    {
        ValidateUnitInterval(expectedScore, nameof(expectedScore));
        return Math.Clamp(1.5m - expectedScore, 0.60m, 1.40m);
    }

    public int CalculateHybridCareerPoints(int pvpBasePoints, decimal expectedScore)
    {
        if (pvpBasePoints < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pvpBasePoints));
        }

        var points = pvpBasePoints * HybridCareerFactor * GetChallengeMultiplier(expectedScore);
        return decimal.ToInt32(Math.Round(points, 0, MidpointRounding.AwayFromZero));
    }

    public decimal CalculatePvePointsPerHuman(
        IReadOnlyCollection<int> aiInternalLevels,
        int humanCount,
        ScoringResult result,
        int equivalentWinsInSeason)
    {
        ArgumentNullException.ThrowIfNull(aiInternalLevels);
        if (aiInternalLevels.Count == 0 || humanCount < 1 || equivalentWinsInSeason < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(humanCount));
        }

        var total = aiInternalLevels.Sum(level => result == ScoringResult.Victory
            ? GetAiRule(level).VictoryPoints
            : GetAiRule(level).ValidDefeatPoints);
        var repetitionFactor = result == ScoringResult.Victory
            ? GetPveRepetitionFactor(equivalentWinsInSeason)
            : 1m;
        return Math.Round(((decimal)total / humanCount) * repetitionFactor, 2, MidpointRounding.AwayFromZero);
    }

    public decimal GetPveRepetitionFactor(int previousEquivalentWins)
    {
        if (previousEquivalentWins < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(previousEquivalentWins));
        }

        return previousEquivalentWins switch
        {
            < 3 => 1m,
            < 10 => 0.25m,
            _ => 0m
        };
    }

    private static void ValidateUnitInterval(decimal value, string parameterName)
    {
        if (value is < 0m or > 1m)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Value must be between zero and one.");
        }
    }
}
