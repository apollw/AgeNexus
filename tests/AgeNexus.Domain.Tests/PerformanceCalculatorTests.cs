using AgeNexus.Application.MatchPerformance;
using AgeNexus.Domain.MatchPerformance;
using AgeNexus.Domain.Matches;

namespace AgeNexus.Domain.Tests;

public sealed class PerformanceCalculatorTests
{
    private readonly PerformanceCalculator _calculator = new();

    [Fact]
    public void Losing_player_can_be_the_unique_mvp()
    {
        var winner = Input(TeamResult.Victory, 100, 100, 100, 100);
        var defeated = Input(TeamResult.Defeat, 200, 200, 200, 200);

        var result = _calculator.Calculate(new(
            MatchScoringCategory.PurePvp, 1, [winner, defeated]));

        var mvp = result.Players.Single(x => x.PlayerProfileId == defeated.PlayerProfileId);
        Assert.Equal(PerformanceAwardType.Mvp, mvp.AwardType);
        Assert.Equal(2, mvp.BonusPoints);
    }

    [Fact]
    public void Best_defeated_player_receives_one_point_when_close_and_leading_a_pillar()
    {
        var winner = Input(TeamResult.Victory, 190, 200, 200, 200);
        var winningAlly = Input(TeamResult.Victory, 10, 10, 10, 10, winner.TeamId);
        var defeated = Input(TeamResult.Defeat, 200, 170, 170, 170);
        var defeatedAlly = Input(TeamResult.Defeat, 0, 0, 0, 0, defeated.TeamId);

        var result = _calculator.Calculate(new(
            MatchScoringCategory.PurePvp, 2, [winner, winningAlly, defeated, defeatedAlly]));

        Assert.Equal(PerformanceAwardType.Mvp,
            result.Players.Single(x => x.PlayerProfileId == winner.PlayerProfileId).AwardType);
        var highlight = result.Players.Single(x => x.PlayerProfileId == defeated.PlayerProfileId);
        Assert.Equal(PerformanceAwardType.DefeatedTeamHighlight, highlight.AwardType);
        Assert.Equal(1, highlight.BonusPoints);
    }

    [Fact]
    public void Hybrid_mvp_is_capped_at_one_career_point()
    {
        var winner = Input(TeamResult.Victory, 200, 200, 200, 200);
        var defeated = Input(TeamResult.Defeat, 100, 100, 100, 100);

        var result = _calculator.Calculate(new(
            MatchScoringCategory.HybridPvp, 1, [winner, defeated]));

        Assert.Equal(1, result.Players.Single(x => x.AwardType == PerformanceAwardType.Mvp).BonusPoints);
    }

    [Fact]
    public void Pve_keeps_mvp_badge_without_career_bonus()
    {
        var first = Input(TeamResult.Victory, 200, 200, 200, 200);
        var second = Input(TeamResult.Victory, 100, 100, 100, 100, first.TeamId);

        var result = _calculator.Calculate(new(
            MatchScoringCategory.PurePve, 2, [first, second]));

        var mvp = result.Players.Single(x => x.AwardType == PerformanceAwardType.Mvp);
        Assert.Equal(0, mvp.BonusPoints);
    }

    [Fact]
    public void Solo_pve_does_not_award_an_automatic_mvp()
    {
        var player = Input(TeamResult.Victory, 200, 200, 200, 200);

        var result = _calculator.Calculate(new(
            MatchScoringCategory.PurePve, 1, [player]));

        var score = Assert.Single(result.Players);
        Assert.Equal(PerformanceAwardType.None, score.AwardType);
        Assert.Equal(0, score.BonusPoints);
    }

    [Fact]
    public void Complete_statistics_require_every_core_field()
    {
        var statistic = new PlayerMatchStatistics(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            StatisticValueOrigin.Manual,
            new MatchStatisticValues(1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1));

        Assert.False(statistic.IsComplete);
        statistic.Apply(new MatchStatisticValues(1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1),
            StatisticValueOrigin.Manual);
        Assert.True(statistic.IsComplete);
    }

    private static PerformancePlayerInput Input(
        TeamResult result,
        int military,
        int economy,
        int technology,
        int society,
        Guid? teamId = null) =>
        new(Guid.NewGuid(), teamId ?? Guid.NewGuid(), result, military, economy, technology, society);
}
