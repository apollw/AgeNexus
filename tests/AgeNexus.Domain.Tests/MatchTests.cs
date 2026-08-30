using AgeNexus.Domain.Common;
using AgeNexus.Domain.Matches;

namespace AgeNexus.Domain.Tests;

public sealed class MatchTests
{
    [Theory]
    [InlineData(1, 1, "1x1")]
    [InlineData(2, 2, "2x2")]
    [InlineData(3, 2, "3x2")]
    [InlineData(4, 4, "4x4")]
    public void Derives_competitive_format_from_human_team_sizes(int firstSize, int secondSize, string expected)
    {
        var match = CreateMatch(MatchType.PlayerVersusPlayer);
        var firstTeam = match.AddTeam(Guid.NewGuid());
        var secondTeam = match.AddTeam(Guid.NewGuid());

        AddHumans(match, firstTeam.Id, firstSize);
        AddHumans(match, secondTeam.Id, secondSize);

        Assert.Equal(expected, match.CompetitiveFormatLabel);
        match.Submit();
        Assert.Equal(MatchStatus.Submitted, match.Status);
    }

    [Fact]
    public void Supports_humans_against_multiple_ai_participants()
    {
        var match = CreateMatch(MatchType.HumansVersusAi);
        var humans = match.AddTeam(Guid.NewGuid());
        var computers = match.AddTeam(Guid.NewGuid());
        AddHumans(match, humans.Id, 2);
        match.AddParticipant(computers.Id, MatchParticipant.Ai(Guid.NewGuid(), Guid.NewGuid()));
        match.AddParticipant(computers.Id, MatchParticipant.Ai(Guid.NewGuid(), Guid.NewGuid()));
        match.AddParticipant(computers.Id, MatchParticipant.Ai(Guid.NewGuid(), Guid.NewGuid()));

        match.Submit();

        Assert.Equal(2, humans.HumanCount);
        Assert.Equal(3, computers.AiCount);
        Assert.Null(match.CompetitiveFormatLabel);
    }

    [Fact]
    public void Rejects_ai_in_a_pvp_match()
    {
        var match = CreateMatch(MatchType.PlayerVersusPlayer);
        var humans = match.AddTeam(Guid.NewGuid());
        var computers = match.AddTeam(Guid.NewGuid());
        AddHumans(match, humans.Id, 1);
        match.AddParticipant(computers.Id, MatchParticipant.Ai(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Throws<DomainRuleException>(match.Submit);
    }

    [Fact]
    public void Rejects_the_same_player_profile_twice()
    {
        var match = CreateMatch(MatchType.PlayerVersusPlayer);
        var firstTeam = match.AddTeam(Guid.NewGuid());
        var secondTeam = match.AddTeam(Guid.NewGuid());
        var playerId = Guid.NewGuid();
        match.AddParticipant(firstTeam.Id, MatchParticipant.Human(Guid.NewGuid(), playerId));

        Assert.Throws<DomainRuleException>(() =>
            match.AddParticipant(secondTeam.Id, MatchParticipant.Human(Guid.NewGuid(), playerId)));
    }

    [Fact]
    public void Locks_composition_after_submission()
    {
        var match = CreateMatch(MatchType.PlayerVersusPlayer);
        var firstTeam = match.AddTeam(Guid.NewGuid());
        var secondTeam = match.AddTeam(Guid.NewGuid());
        AddHumans(match, firstTeam.Id, 1);
        AddHumans(match, secondTeam.Id, 1);
        match.Submit();

        Assert.Throws<DomainRuleException>(() => match.AddTeam(Guid.NewGuid()));
    }

    private static Match CreateMatch(MatchType type) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        DateTimeOffset.UtcNow,
        type,
        MatchNature.Casual);

    private static void AddHumans(Match match, Guid teamId, int count)
    {
        for (var index = 0; index < count; index++)
        {
            match.AddParticipant(teamId, MatchParticipant.Human(Guid.NewGuid(), Guid.NewGuid()));
        }
    }
}
