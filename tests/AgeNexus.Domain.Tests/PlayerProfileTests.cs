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

    [Fact]
    public void Uploaded_avatar_can_be_replaced_and_removed()
    {
        var profile = new PlayerProfile(Guid.NewGuid(), "Jogador");

        profile.UpdateAvatar("https://example.com/profile/avatar.webp");
        Assert.Equal("https://example.com/profile/avatar.webp", profile.AvatarUrl);

        profile.UpdateAvatar(null);
        Assert.Null(profile.AvatarUrl);
    }

    [Fact]
    public void Historical_profile_claim_requires_an_administrator_decision()
    {
        var profileId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var administratorId = Guid.NewGuid();
        var claim = new PlayerProfileClaim(Guid.NewGuid(), userId, profileId, DateTimeOffset.UtcNow);

        Assert.Equal(PlayerProfileClaimStatus.Pending, claim.Status);

        claim.Approve(administratorId, DateTimeOffset.UtcNow);

        Assert.Equal(PlayerProfileClaimStatus.Approved, claim.Status);
        Assert.Equal(administratorId, claim.DecidedByApplicationUserId);
        Assert.Throws<DomainRuleException>(() => claim.Reject(administratorId, DateTimeOffset.UtcNow));
    }
}
