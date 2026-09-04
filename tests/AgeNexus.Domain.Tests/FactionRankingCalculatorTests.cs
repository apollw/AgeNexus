using AgeNexus.Application.Queries;

namespace AgeNexus.Domain.Tests;

public sealed class FactionRankingCalculatorTests
{
    [Fact]
    public void CalculateStrengthIndex_RequiresAValidSample()
    {
        Assert.Equal(0m, FactionRankingCalculator.CalculateStrengthIndex(0, 0, 0));
        Assert.Equal(0m, FactionRankingCalculator.CalculateStrengthIndex(1, 2, 0));
    }

    [Fact]
    public void CalculateStrengthIndex_ValuesWinsAndHalfOfDraws()
    {
        var victory = FactionRankingCalculator.CalculateStrengthIndex(10, 6, 0);
        var draw = FactionRankingCalculator.CalculateStrengthIndex(10, 5, 1);
        var defeat = FactionRankingCalculator.CalculateStrengthIndex(10, 5, 0);

        Assert.True(victory > draw);
        Assert.True(draw > defeat);
    }

    [Fact]
    public void CalculateStrengthIndex_RewardsAConfirmedSampleOverOnePerfectResult()
    {
        var oneVictory = FactionRankingCalculator.CalculateStrengthIndex(1, 1, 0);
        var repeatedPerformance = FactionRankingCalculator.CalculateStrengthIndex(10, 6, 0);

        Assert.True(repeatedPerformance > oneVictory);
    }
}
