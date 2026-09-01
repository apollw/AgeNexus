using AgeNexus.Domain.Competition;
using System.Globalization;

namespace AgeNexus.Domain.Tests;

public sealed class ScoringRuleSetTests
{
    private readonly ScoringRuleSet _rules = new();

    [Theory]
    [InlineData(1, 100, 50, 25)]
    [InlineData(2, 75, 38, 20)]
    [InlineData(3, 60, 30, 18)]
    [InlineData(4, 50, 25, 15)]
    public void Uses_career_table_by_largest_team(
        int teamSize,
        int victory,
        int draw,
        int defeat)
    {
        Assert.Equal(victory, _rules.GetPvpCareerBase(teamSize, ScoringResult.Victory));
        Assert.Equal(draw, _rules.GetPvpCareerBase(teamSize, ScoringResult.Draw));
        Assert.Equal(defeat, _rules.GetPvpCareerBase(teamSize, ScoringResult.Defeat));
    }

    [Fact]
    public void Rewards_smaller_team_in_asymmetric_victory()
    {
        Assert.Equal(1.25m, _rules.GetAsymmetricCareerMultiplier(2, 3, ScoringResult.Victory));
        Assert.Equal(0.85m, _rules.GetAsymmetricCareerMultiplier(3, 2, ScoringResult.Victory));
    }

    [Theory]
    [InlineData(0, "0.85")]
    [InlineData(1, "0.85")]
    [InlineData(2, "0.75")]
    [InlineData(3, "0.625")]
    public void Reduces_hybrid_rating_by_ai_proportion(int aiCount, string expected)
    {
        Assert.Equal(
            decimal.Parse(expected, CultureInfo.InvariantCulture),
            _rules.GetHybridRatingFactor(aiCount, 4));
    }

    [Fact]
    public void Calculates_pve_from_sum_of_ai_values_divided_by_humans()
    {
        var points = _rules.CalculatePvePointsPerHuman(
            [3, 5],
            humanCount: 2,
            ScoringResult.Victory,
            equivalentWinsInSeason: 0);

        Assert.Equal(52.5m, points);
    }

    [Theory]
    [InlineData(0, "1")]
    [InlineData(2, "1")]
    [InlineData(3, "0.25")]
    [InlineData(9, "0.25")]
    [InlineData(10, "0")]
    public void Applies_seasonal_pve_repetition_factor(int previousWins, string expected)
    {
        Assert.Equal(
            decimal.Parse(expected, CultureInfo.InvariantCulture),
            _rules.GetPveRepetitionFactor(previousWins));
    }
}
