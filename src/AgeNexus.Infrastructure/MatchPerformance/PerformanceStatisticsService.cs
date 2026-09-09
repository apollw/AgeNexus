using System.Text.Json;
using AgeNexus.Application.MatchPerformance;
using AgeNexus.Application.Matches;
using AgeNexus.Domain.Common;
using AgeNexus.Domain.Competition;
using AgeNexus.Domain.MatchPerformance;
using AgeNexus.Domain.Matches;
using AgeNexus.Infrastructure.Persistence;
using AgeNexus.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AgeNexus.Infrastructure.MatchPerformance;

public sealed class PerformanceStatisticsService(
    AgeNexusDbContext database,
    AgeNexusDbContextFactory databaseFactory,
    IPerformanceCalculator calculator,
    IMatchWorkflowService matchWorkflow,
    AccountService accounts,
    IConfiguration configuration) : IPerformanceStatisticsService
{
    private const string ManualMvpRuleVersion = "2026.09-manual-mvp.1";

    public async Task<PerformanceReportView?> GetAsync(
        Guid matchId,
        CancellationToken cancellationToken = default)
    {
        await using var matchDatabase = databaseFactory.CreateDbContext();
        await using var reportDatabase = databaseFactory.CreateDbContext();
        var matchTask = LoadMatchForReadAsync(matchDatabase, matchId, cancellationToken);
        var reportTask = reportDatabase.MatchStatisticsReports.AsNoTracking()
            .SingleOrDefaultAsync(x => x.MatchId == matchId, cancellationToken);
        await Task.WhenAll(matchTask, reportTask);
        var match = await matchTask;
        if (match is null)
        {
            return null;
        }

        var humans = match.Teams.OrderBy(team => team.Position).SelectMany(team => team.Participants
            .Where(x => x.Type == ParticipantType.Human)
            .Select(x => new { Team = team, PlayerId = x.PlayerProfileId!.Value })).ToArray();
        var playerIds = humans.Select(x => x.PlayerId).ToArray();
        var report = await reportTask;

        await using var namesDatabase = databaseFactory.CreateDbContext();
        await using var statisticsDatabase = databaseFactory.CreateDbContext();
        await using var scoresDatabase = databaseFactory.CreateDbContext();
        await using var confirmationsDatabase = databaseFactory.CreateDbContext();
        var namesTask = namesDatabase.PlayerProfiles.AsNoTracking().Where(x => playerIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);
        var statisticsTask = report is null
            ? Task.FromResult(Array.Empty<PlayerMatchStatistics>())
            : statisticsDatabase.PlayerMatchStatistics.AsNoTracking()
                .Where(x => x.ReportId == report.Id).ToArrayAsync(cancellationToken);
        var scoresTask = report?.Status != MatchStatisticsStatus.Awarded
            ? Task.FromResult(Array.Empty<PlayerPerformanceScore>())
            : scoresDatabase.PlayerPerformanceScores.AsNoTracking()
                .Where(x => x.ReportId == report.Id).ToArrayAsync(cancellationToken);
        var confirmedTeamsTask = report?.Status is not (MatchStatisticsStatus.Submitted or MatchStatisticsStatus.Confirmed or MatchStatisticsStatus.Awarded)
            ? Task.FromResult(Array.Empty<Guid>())
            : confirmationsDatabase.StatisticsConfirmations.AsNoTracking()
                .Where(x => x.ReportId == report.Id && x.Decision == StatisticsConfirmationDecision.Confirmed)
                .Select(x => x.TeamId).ToArrayAsync(cancellationToken);
        await Task.WhenAll(namesTask, statisticsTask, scoresTask, confirmedTeamsTask);
        var names = await namesTask;
        var statistics = await statisticsTask;
        var scores = await scoresTask;
        var confirmedTeams = await confirmedTeamsTask;
        var statisticLookup = statistics.ToDictionary(x => x.PlayerProfileId);
        var scoreLookup = scores.ToDictionary(x => x.PlayerProfileId);
        var players = humans.Select(x =>
        {
            statisticLookup.TryGetValue(x.PlayerId, out var statistic);
            scoreLookup.TryGetValue(x.PlayerId, out var score);
            return new PerformancePlayerView(
                x.PlayerId,
                x.Team.Id,
                names.GetValueOrDefault(x.PlayerId, "Jogador"),
                x.Team.Result,
                statistic?.Origin,
                statistic?.ToValues() ?? new MatchStatisticValues(),
                score?.Overall,
                score?.AwardType,
                score?.BonusPoints ?? 0);
        }).ToArray();

        return new PerformanceReportView(
            match.Id,
            match.Status.ToString(),
            match.ScoringCategory.ToString(),
            report?.Id,
            report?.Source,
            report?.Status,
            report?.ReplayFileName,
            report?.ExtractorVersion,
            players.Length > 0 && players.All(x => IsComplete(x.Values)),
            players,
            confirmedTeams);
    }

    public async Task<PerformanceOperationResult> SaveManualAsync(
        SavePerformanceReportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var match = await LoadMatchAsync(request.MatchId, cancellationToken);
        if (match is null)
        {
            return PerformanceOperationResult.Failure("MatchNotFound");
        }

        if (!await accounts.IsAdministratorProfileAsync(request.SubmittedByPlayerProfileId, cancellationToken))
        {
            return PerformanceOperationResult.Failure("NotAuthorized");
        }

        var participantTeams = HumanParticipantTeams(match);
        if (request.Players.Count != participantTeams.Count ||
            request.Players.Select(x => x.PlayerProfileId).Distinct().Count() != request.Players.Count ||
            request.Players.Any(x => !participantTeams.ContainsKey(x.PlayerProfileId)))
        {
            return PerformanceOperationResult.Failure("StatisticsMustIncludeEveryHuman");
        }

        try
        {
            var report = await database.MatchStatisticsReports.SingleOrDefaultAsync(
                x => x.MatchId == request.MatchId,
                cancellationToken);
            if (report is null)
            {
                if (request.Source is MatchStatisticsSource.Replay or MatchStatisticsSource.ReplayWithManualCompletion)
                {
                    return PerformanceOperationResult.Failure("ReplayMustBeImported");
                }

                report = new MatchStatisticsReport(
                    Guid.NewGuid(), request.MatchId, request.SubmittedByPlayerProfileId,
                    request.Source, DateTimeOffset.UtcNow,
                    coverageDetails: request.Source == MatchStatisticsSource.JsonImport ? request.ImportDetails : null);
                database.MatchStatisticsReports.Add(report);
            }
            else if (report.Status != MatchStatisticsStatus.Draft)
            {
                return PerformanceOperationResult.Failure("ReportIsLocked");
            }
            else if (request.Source == MatchStatisticsSource.JsonImport && request.ImportDetails is not null)
            {
                report.ApplyJsonImport(request.ImportDetails);
            }

            var existing = await database.PlayerMatchStatistics
                .Where(x => x.ReportId == report.Id)
                .ToDictionaryAsync(x => x.PlayerProfileId, cancellationToken);
            foreach (var submitted in request.Players)
            {
                var origin = request.Source switch
                {
                    MatchStatisticsSource.ScreenshotTranscription => StatisticValueOrigin.Screenshot,
                    MatchStatisticsSource.JsonImport => StatisticValueOrigin.JsonImport,
                    _ => submitted.Origin
                };
                if (existing.TryGetValue(submitted.PlayerProfileId, out var statistic))
                {
                    statistic.Apply(submitted.Values, origin);
                }
                else
                {
                    database.PlayerMatchStatistics.Add(new PlayerMatchStatistics(
                        Guid.NewGuid(), report.Id, match.Id, participantTeams[submitted.PlayerProfileId],
                        submitted.PlayerProfileId, origin, submitted.Values));
                }
            }

            report.MarkManualCompletion();
            await database.SaveChangesAsync(cancellationToken);
            return PerformanceOperationResult.Success(report.Id);
        }
        catch (DomainRuleException)
        {
            return PerformanceOperationResult.Failure("InvalidStatistics");
        }
    }

    public async Task<PerformanceOperationResult> SubmitAsync(
        Guid reportId,
        Guid playerProfileId,
        CancellationToken cancellationToken = default)
    {
        var report = await database.MatchStatisticsReports.SingleOrDefaultAsync(x => x.Id == reportId, cancellationToken);
        if (report is null)
        {
            return PerformanceOperationResult.Failure("ReportNotFound");
        }

        if (report.Status == MatchStatisticsStatus.Submitted)
        {
            return PerformanceOperationResult.Success(reportId, alreadyApplied: true);
        }

        var match = await LoadMatchAsync(report.MatchId, cancellationToken);
        if (match is null)
        {
            return PerformanceOperationResult.Failure("MatchNotFound");
        }
        if (!await accounts.IsAdministratorProfileAsync(playerProfileId, cancellationToken))
        {
            return PerformanceOperationResult.Failure("NotAuthorized");
        }

        var humanCount = HumanParticipantTeams(match).Count;
        var statistics = await database.PlayerMatchStatistics.Where(x => x.ReportId == reportId).ToArrayAsync(cancellationToken);
        var humanTeamIds = match.Teams.Where(x => x.HumanCount > 0).Select(x => x.Id).ToArray();
        if (SingleAdministratorMode && humanTeamIds.Any(teamId =>
                statistics.Count(x => x.TeamId == teamId && x.IsTeamMvp) != 1))
        {
            return PerformanceOperationResult.Failure("TeamMvpRequired");
        }

        try
        {
            var now = DateTimeOffset.UtcNow;
            report.Submit(now, statistics.Length == humanCount && statistics.All(x => x.IsComplete));
            if (SingleAdministratorMode)
            {
                report.MarkConfirmed(now);
                if (match.Status == MatchStatus.AwaitingConfirmation)
                {
                    match.MarkConfirmed();
                }
            }
            await database.SaveChangesAsync(cancellationToken);

            if (SingleAdministratorMode && match.Status == MatchStatus.Confirmed)
            {
                var validation = await matchWorkflow.ValidateAsync(match.Id, cancellationToken);
                if (!validation.Succeeded)
                {
                    return PerformanceOperationResult.Failure("MatchCouldNotBeValidated");
                }
            }

            return PerformanceOperationResult.Success(reportId);
        }
        catch (DomainRuleException)
        {
            return PerformanceOperationResult.Failure("ReportIsIncomplete");
        }
    }

    public async Task<PerformanceOperationResult> ConfirmAsync(
        Guid reportId,
        Guid playerProfileId,
        StatisticsConfirmationDecision decision,
        CancellationToken cancellationToken = default)
    {
        var report = await database.MatchStatisticsReports.SingleOrDefaultAsync(x => x.Id == reportId, cancellationToken);
        if (report is null)
        {
            return PerformanceOperationResult.Failure("ReportNotFound");
        }

        var match = await LoadMatchAsync(report.MatchId, cancellationToken);
        if (match is null)
        {
            return PerformanceOperationResult.Failure("MatchNotFound");
        }

        var participantTeams = HumanParticipantTeams(match);
        if (!participantTeams.TryGetValue(playerProfileId, out var teamId))
        {
            return PerformanceOperationResult.Failure("PlayerNotInMatch");
        }

        if (report.Status is not (MatchStatisticsStatus.Submitted or MatchStatisticsStatus.Confirmed))
        {
            return PerformanceOperationResult.Failure("ReportCannotBeConfirmed");
        }

        var existing = await database.StatisticsConfirmations.SingleOrDefaultAsync(
            x => x.ReportId == reportId && x.TeamId == teamId,
            cancellationToken);
        if (existing is not null)
        {
            return existing.Decision == decision
                ? PerformanceOperationResult.Success(reportId, alreadyApplied: true)
                : PerformanceOperationResult.Failure("TeamAlreadyDecided");
        }

        database.StatisticsConfirmations.Add(new StatisticsConfirmation(
            Guid.NewGuid(), reportId, teamId, playerProfileId, decision, DateTimeOffset.UtcNow));
        if (decision == StatisticsConfirmationDecision.Contested)
        {
            report.Reject();
        }
        else
        {
            var confirmedTeams = await database.StatisticsConfirmations
                .Where(x => x.ReportId == reportId && x.Decision == StatisticsConfirmationDecision.Confirmed)
                .Select(x => x.TeamId).ToListAsync(cancellationToken);
            confirmedTeams.Add(teamId);
            var humanTeams = match.Teams.Where(x => x.HumanCount > 0).Select(x => x.Id).ToArray();
            if (humanTeams.All(confirmedTeams.Contains))
            {
                report.MarkConfirmed(DateTimeOffset.UtcNow);
            }
        }

        await database.SaveChangesAsync(cancellationToken);
        return PerformanceOperationResult.Success(reportId);
    }

    public async Task<PerformanceOperationResult> FinalizeAsync(
        Guid reportId,
        CancellationToken cancellationToken = default)
    {
        var report = await database.MatchStatisticsReports.SingleOrDefaultAsync(x => x.Id == reportId, cancellationToken);
        if (report is null)
        {
            return PerformanceOperationResult.Failure("ReportNotFound");
        }

        if (report.Status == MatchStatisticsStatus.Awarded)
        {
            return PerformanceOperationResult.Success(reportId, alreadyApplied: true);
        }

        var match = await LoadMatchAsync(report.MatchId, cancellationToken);
        if (match is null)
        {
            return PerformanceOperationResult.Failure("MatchNotFound");
        }

        if (SingleAdministratorMode)
        {
            var stateChanged = false;
            if (report.Status == MatchStatisticsStatus.Submitted)
            {
                report.MarkConfirmed(DateTimeOffset.UtcNow);
                stateChanged = true;
            }

            if (match.Status == MatchStatus.AwaitingConfirmation)
            {
                match.MarkConfirmed();
                stateChanged = true;
            }

            if (stateChanged)
            {
                await database.SaveChangesAsync(cancellationToken);
            }

            if (match.Status == MatchStatus.Confirmed)
            {
                var validation = await matchWorkflow.ValidateAsync(match.Id, cancellationToken);
                if (!validation.Succeeded)
                {
                    return PerformanceOperationResult.Failure("MatchCouldNotBeValidated");
                }
            }
        }

        if (match?.Status != MatchStatus.Validated)
        {
            return PerformanceOperationResult.Failure("MatchMustBeValidated");
        }

        if (report.Status != MatchStatisticsStatus.Confirmed)
        {
            return PerformanceOperationResult.Failure("ReportMustBeConfirmed");
        }

        var statistics = await database.PlayerMatchStatistics.Where(x => x.ReportId == reportId).ToArrayAsync(cancellationToken);
        if (statistics.Length == 0 || statistics.Any(x => !x.IsComplete))
        {
            return PerformanceOperationResult.Failure("ReportIsIncomplete");
        }

        var teams = match.Teams.ToDictionary(x => x.Id);
        var calculation = calculator.Calculate(new PerformanceCalculationRequest(
            match.ScoringCategory,
            match.Teams.Max(x => x.HumanCount),
            statistics.Select(x => new PerformancePlayerInput(
                x.PlayerProfileId,
                x.TeamId,
                teams[x.TeamId].Result,
                x.MilitaryScore!.Value,
                x.EconomyScore!.Value,
                x.TechnologyScore!.Value,
                x.SocietyScore!.Value)).ToArray()));
        var statisticByPlayer = statistics.ToDictionary(x => x.PlayerProfileId);
        var mvpBonus = match.ScoringCategory switch
        {
            MatchScoringCategory.PurePvp => 2,
            MatchScoringCategory.HybridPvp => 1,
            _ => 0
        };
        var now = DateTimeOffset.UtcNow;
        foreach (var result in calculation.Players)
        {
            var isTeamMvp = statisticByPlayer[result.PlayerProfileId].IsTeamMvp;
            var awardType = isTeamMvp ? PerformanceAwardType.Mvp : PerformanceAwardType.None;
            var bonusPoints = isTeamMvp ? mvpBonus : 0;
            database.PlayerPerformanceScores.Add(new PlayerPerformanceScore(
                Guid.NewGuid(), report.Id, match.Id, result.TeamId, result.PlayerProfileId,
                result.Military, result.Economy, result.Technology, result.Society, result.Overall,
                awardType, bonusPoints, ManualMvpRuleVersion, now));
            if (bonusPoints > 0)
            {
                database.PointEvents.Add(new PointEvent(
                    Guid.NewGuid(), match.Id, result.PlayerProfileId, match.SeasonId,
                    PointScopeKind.PerformanceBonus, bonusPoints, ManualMvpRuleVersion,
                    JsonSerializer.Serialize(new
                    {
                        report.Id,
                        AwardType = awardType,
                        result.Overall,
                        report.Source,
                        FormulaVersion = ManualMvpRuleVersion
                    }),
                    now,
                    $"performance:{report.Id}"));
            }
        }

        report.MarkAwarded(now);
        await database.SaveChangesAsync(cancellationToken);
        return PerformanceOperationResult.Success(reportId);
    }

    public async Task<PerformanceOperationResult> ReopenAsync(
        Guid reportId,
        Guid administratorPlayerProfileId,
        CancellationToken cancellationToken = default)
    {
        var report = await database.MatchStatisticsReports.SingleOrDefaultAsync(
            x => x.Id == reportId, cancellationToken);
        if (report is null)
        {
            return PerformanceOperationResult.Failure("ReportNotFound");
        }

        var match = await LoadMatchAsync(report.MatchId, cancellationToken);
        if (match is null || !await accounts.IsAdministratorProfileAsync(
                administratorPlayerProfileId, cancellationToken))
        {
            return PerformanceOperationResult.Failure("NotAuthorized");
        }

        try
        {
            var scores = await database.PlayerPerformanceScores
                .Where(x => x.ReportId == reportId).ToArrayAsync(cancellationToken);
            var bonusEvents = await database.PointEvents
                .Where(x => x.MatchId == match.Id && x.Scope == PointScopeKind.PerformanceBonus)
                .ToArrayAsync(cancellationToken);
            var confirmations = await database.StatisticsConfirmations
                .Where(x => x.ReportId == reportId).ToArrayAsync(cancellationToken);
            database.PlayerPerformanceScores.RemoveRange(scores);
            database.PointEvents.RemoveRange(bonusEvents);
            database.StatisticsConfirmations.RemoveRange(confirmations);
            report.ReopenForAdministrativeCorrection();
            await database.SaveChangesAsync(cancellationToken);
            return PerformanceOperationResult.Success(reportId);
        }
        catch (DomainRuleException)
        {
            return PerformanceOperationResult.Failure("ReportCannotBeReopened");
        }
    }

    private Task<Match?> LoadMatchAsync(Guid matchId, CancellationToken cancellationToken) =>
        database.Matches.Include(x => x.Teams).ThenInclude(x => x.Participants)
            .SingleOrDefaultAsync(x => x.Id == matchId, cancellationToken);

    private static Task<Match?> LoadMatchForReadAsync(
        AgeNexusDbContext context,
        Guid matchId,
        CancellationToken cancellationToken) =>
        context.Matches.AsNoTracking().Include(x => x.Teams).ThenInclude(x => x.Participants)
            .SingleOrDefaultAsync(x => x.Id == matchId, cancellationToken);

    private static Dictionary<Guid, Guid> HumanParticipantTeams(Match match) =>
        match.Teams.SelectMany(team => team.Participants
            .Where(x => x.Type == ParticipantType.Human)
            .Select(x => new { PlayerId = x.PlayerProfileId!.Value, TeamId = team.Id }))
            .ToDictionary(x => x.PlayerId, x => x.TeamId);

    private bool SingleAdministratorMode =>
        configuration.GetValue("OperatingMode:SingleAdministrator", true);

    private static bool IsComplete(MatchStatisticValues values) =>
        values.UnitsKilled.HasValue && values.UnitsLost.HasValue && values.BuildingsDestroyed.HasValue &&
        values.BuildingsLost.HasValue && values.LargestArmy.HasValue && values.PeakVillagers.HasValue &&
        values.FoodCollected.HasValue && values.WoodCollected.HasValue && values.GoldCollected.HasValue &&
        values.StoneCollected.HasValue && values.MilitaryScore.HasValue && values.EconomyScore.HasValue &&
        values.TechnologyScore.HasValue && values.SocietyScore.HasValue && values.TotalScore.HasValue;

}
