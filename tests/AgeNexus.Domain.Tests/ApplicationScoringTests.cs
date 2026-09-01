using AgeNexus.Application.Ratings;
using AgeNexus.Domain.Competition;
using AgeNexus.Domain.EvidenceAndModeration;
using AgeNexus.Domain.Matches;

namespace AgeNexus.Domain.Tests;

public sealed class ApplicationScoringTests
{
    private readonly ScoringRuleSet _rules = new();

    [Fact]
    public void Elo_calculator_does_not_award_ai_participants()
    {
        var humanA = Guid.NewGuid();
        var humanB = Guid.NewGuid();
        var calculator = new EloRatingCalculator(_rules);
        var calculation = calculator.Calculate(new RatingCalculationRequest(
            [
                new RatedTeam(Guid.NewGuid(), 1m,
                [
                    new RatedParticipant(humanA, 1000m, 20),
                    new RatedParticipant(null, 1000m, 0, IsAi: true)
                ]),
                new RatedTeam(Guid.NewGuid(), 0m,
                [
                    new RatedParticipant(humanB, 1000m, 20)
                ])
            ],
            ModalityWeight: 0.75m,
            CategoryFactor: _rules.GetHybridRatingFactor(1, 3)));

        Assert.Equal(2, calculation.Deltas.Count);
        Assert.DoesNotContain(calculation.Deltas, x => x.PlayerId == Guid.Empty);
    }

    [Fact]
    public void Basic_pve_evidence_awards_forty_percent_and_respects_cap()
    {
        var player = Guid.NewGuid();
        var calculator = new PvePointCalculator(_rules);
        var calculation = calculator.Calculate(new PvePointCalculationRequest(
            [player],
            [5],
            ScoringResult.Victory,
            PreviousEquivalentWins: 0,
            EvidenceLevel.Basic,
            BasicEvidencePointsAlreadyAwarded: 140m));

        var award = Assert.Single(calculation.Awards);
        Assert.Equal(10m, award.PvePoints);
        Assert.Equal(3.5m, award.CareerPoints);
    }

    [Fact]
    public void Hybrid_career_uses_reduced_base_and_challenge()
    {
        var player = Guid.NewGuid();
        var calculator = new CareerPointCalculator(_rules);
        var calculation = calculator.Calculate(new CareerPointCalculationRequest(
            MatchScoringCategory.HybridPvp,
            LargestHumanTeamSize: 1,
            [new CareerTeam([player], 1, 1, ScoringResult.Victory, ExpectedScore: 0.5m)]));

        Assert.Equal(70, Assert.Single(calculation.Awards).Points);
    }
}
