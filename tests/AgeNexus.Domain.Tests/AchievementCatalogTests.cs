using AgeNexus.Application.Queries;

namespace AgeNexus.Domain.Tests;

public sealed class AchievementCatalogTests
{
    [Fact]
    public void Catalog_contains_fifty_unique_well_formed_achievements()
    {
        Assert.Equal(50, AchievementCatalog.All.Count);
        Assert.Equal(50, AchievementCatalog.All.Select(x => x.Key).Distinct().Count());
        Assert.Equal(50, AchievementCatalog.All.Select(x => x.Title).Distinct().Count());
        Assert.All(AchievementCatalog.All, achievement =>
        {
            Assert.False(string.IsNullOrWhiteSpace(achievement.Category));
            Assert.False(string.IsNullOrWhiteSpace(achievement.Description));
            Assert.False(string.IsNullOrWhiteSpace(achievement.Icon));
            Assert.True(achievement.Target > 0);
        });
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
