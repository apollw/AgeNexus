using AgeNexus.Application.Queries;

namespace AgeNexus.Domain.Tests;

public sealed class AchievementCatalogTests
{
    [Fact]
    public void Catalog_contains_seventy_unique_well_formed_achievements()
    {
        Assert.Equal(70, AchievementCatalog.All.Count);
        Assert.Equal(70, AchievementCatalog.All.Select(x => x.Key).Distinct().Count());
        Assert.Equal(70, AchievementCatalog.All.Select(x => x.Title).Distinct().Count());
        Assert.All(AchievementCatalog.All, achievement =>
        {
            Assert.False(string.IsNullOrWhiteSpace(achievement.Category));
            Assert.False(string.IsNullOrWhiteSpace(achievement.Description));
            Assert.False(string.IsNullOrWhiteSpace(achievement.Icon));
            Assert.True(achievement.Target > 0);
        });
    }

    [Fact]
    public void Long_term_tiers_are_at_least_four_times_the_previous_ceiling()
    {
        var longTermKeys = new HashSet<string>
        {
            "four-hundred-battles", "two-thousand-battles", "two-hundred-victories",
            "thousand-victories", "twenty-thousand-kills", "hundred-thousand-kills",
            "four-thousand-buildings", "five-hundred-thousand-food", "five-hundred-thousand-wood",
            "five-hundred-thousand-gold", "two-hundred-fifty-thousand-stone",
            "fifty-thousand-trade-gold", "fifty-thousand-relic-gold", "two-thousand-conversions",
            "two-thousand-researches", "two-hundred-castles", "forty-wonders",
            "two-hundred-relics", "forty-mvp", "forty-survivals"
        };

        foreach (var achievement in AchievementCatalog.All.Where(x => longTermKeys.Contains(x.Key)))
        {
            var previousTarget = AchievementCatalog.All
                .Where(x => x.Metric == achievement.Metric && x.Target < achievement.Target)
                .Max(x => x.Target);
            Assert.True(achievement.Target >= previousTarget * 4,
                $"{achievement.Key} deveria exigir ao menos quatro vezes a meta anterior.");
        }
    }

    [Fact]
    public void Achievement_tiers_are_ordered_by_increasing_target()
    {
        foreach (var metric in AchievementCatalog.All.GroupBy(x => x.Metric))
        {
            var targets = metric.Select(x => x.Target).ToArray();
            Assert.Equal(targets.Order().ToArray(), targets);
        }
    }
}
