using AgeNexus.Domain.Common;
using AgeNexus.Domain.Players;

namespace AgeNexus.Domain.Tests;

public sealed class PlayerProfileTests
{
    [Fact]
    public void Historical_profile_can_be_linked_to_an_account_later()
    {
        var profile = new PlayerProfile(Guid.NewGuid(), "Jogador histórico");
        var userId = Guid.NewGuid();

        profile.LinkToUser(userId);

        Assert.True(profile.HasUserAccount);
        Assert.Equal(userId, profile.ApplicationUserId);
    }

    [Fact]
    public void Linked_profile_cannot_be_claimed_by_another_account()
    {
        var profile = new PlayerProfile(Guid.NewGuid(), "Jogador", Guid.NewGuid());

        Assert.Throws<DomainRuleException>(() => profile.LinkToUser(Guid.NewGuid()));
    }
}
