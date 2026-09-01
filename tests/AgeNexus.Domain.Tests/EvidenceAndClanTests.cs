using AgeNexus.Domain.Clans;
using AgeNexus.Domain.Common;
using AgeNexus.Domain.Competition;
using AgeNexus.Domain.EvidenceAndModeration;

namespace AgeNexus.Domain.Tests;

public sealed class EvidenceAndClanTests
{
    [Fact]
    public void Challenge_ticket_is_single_use_and_time_limited()
    {
        var issuedAt = DateTimeOffset.UtcNow;
        var ticket = new ChallengeTicket(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "A2K9",
            "config-1",
            issuedAt,
            TimeSpan.FromMinutes(30));

        Assert.True(ticket.IsValidAt(issuedAt.AddMinutes(20)));
        ticket.Consume(issuedAt.AddMinutes(20));
        Assert.False(ticket.IsValidAt(issuedAt.AddMinutes(21)));
        Assert.Throws<DomainRuleException>(() => ticket.Consume(issuedAt.AddMinutes(21)));
    }

    [Fact]
    public void Replay_requires_sha256_fingerprint()
    {
        Assert.Throws<DomainRuleException>(() => new MatchEvidence(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            EvidenceKind.Replay,
            DateTimeOffset.UtcNow,
            objectKey: "replays/match.aoe2record"));
    }

    [Theory]
    [InlineData(EvidenceLevel.None, "0")]
    [InlineData(EvidenceLevel.Basic, "0.40")]
    [InlineData(EvidenceLevel.Verified, "1")]
    [InlineData(EvidenceLevel.Audited, "1")]
    public void Evidence_level_controls_pve_award(EvidenceLevel level, string expected)
    {
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture),
            EvidencePolicy.GetPvePointFactor(level));
    }

    [Fact]
    public void Clan_requires_half_of_human_team_for_representation()
    {
        Assert.Equal(0m, ClanScoringRules.GetRepresentationFactor(1, 3));
        Assert.Equal(0.50m, ClanScoringRules.GetRepresentationFactor(1, 2));
        Assert.Equal(0.75m, ClanScoringRules.GetRepresentationFactor(3, 4));
    }

    [Fact]
    public void Hybrid_clan_prestige_uses_reduced_factor()
    {
        var points = ClanScoringRules.CalculatePrestige(
            ClanScoringRules.GetPrestigeBase(2, ScoringResult.Victory),
            representationFactor: 0.75m,
            hybridPvp: true,
            challengeMultiplier: 1m);

        Assert.Equal(45m, points);
    }
}
