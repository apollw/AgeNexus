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

    [Fact]
    public void Public_profile_can_be_personalized()
    {
        var profile = new PlayerProfile(Guid.NewGuid(), "Jogador");

        profile.UpdatePublicProfile(
            "  Mestre Meeple  ",
            "  Jogos de estratégia e café.  ",
            "  Fortaleza, CE  ",
            "https://example.com/avatar.png");

        Assert.Equal("Mestre Meeple", profile.DisplayName);
        Assert.Equal("Jogos de estratégia e café.", profile.Bio);
        Assert.Equal("Fortaleza, CE", profile.Location);
        Assert.Equal("https://example.com/avatar.png", profile.AvatarUrl);
    }

    [Fact]
    public void Avatar_rejects_non_http_urls()
    {
        var profile = new PlayerProfile(Guid.NewGuid(), "Jogador");

        Assert.Throws<DomainRuleException>(() =>
            profile.UpdatePublicProfile("Jogador", null, null, "javascript:alert(1)"));
    }
}
