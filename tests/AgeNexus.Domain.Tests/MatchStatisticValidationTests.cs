using AgeNexus.Domain.Common;
using AgeNexus.Domain.MatchPerformance;

namespace AgeNexus.Domain.Tests;

public sealed class MatchStatisticValidationTests
{
    [Fact]
    public void Statistics_reject_age_times_out_of_chronological_order()
    {
        var values = new MatchStatisticValues(
            FeudalAgeSeconds: 10 * 60 * 60,
            CastleAgeSeconds: 20 * 60,
            ImperialAgeSeconds: 40 * 60);

        Assert.Throws<DomainRuleException>(() => CreateStatistics(values));
    }

    [Fact]
    public void Statistics_accept_short_but_chronologically_valid_age_times()
    {
        var values = new MatchStatisticValues(
            FeudalAgeSeconds: 3 * 60 + 4,
            CastleAgeSeconds: 18 * 60,
            ImperialAgeSeconds: 32 * 60);

        var statistics = CreateStatistics(values);

        Assert.Equal(3 * 60 + 4, statistics.FeudalAgeSeconds);
    }

    private static PlayerMatchStatistics CreateStatistics(MatchStatisticValues values) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        null,
        StatisticValueOrigin.Manual,
        values);
}
