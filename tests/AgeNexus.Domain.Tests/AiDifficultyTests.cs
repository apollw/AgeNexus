using AgeNexus.Domain.Common;
using AgeNexus.Domain.GameCatalog;

namespace AgeNexus.Domain.Tests;

public sealed class AiDifficultyTests
{
    [Fact]
    public void Accepts_all_age2_difficulty_levels()
    {
        var difficulty = new AiDifficulty(Guid.NewGuid(), Guid.NewGuid(), "Extremo", 6);

        Assert.Equal(6, difficulty.InternalLevel);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void Rejects_levels_outside_the_catalog(int internalLevel)
    {
        Assert.Throws<DomainRuleException>(() =>
            new AiDifficulty(Guid.NewGuid(), Guid.NewGuid(), "Inválido", internalLevel));
    }
}
