using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgeNexus.Application.MatchPerformance;
using AgeNexus.Application.Matches;
using AgeNexus.Domain.Common;
using AgeNexus.Domain.Competition;
using AgeNexus.Domain.MatchPerformance;
using AgeNexus.Domain.Matches;
using AgeNexus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AgeNexus.Infrastructure.MatchPerformance;

public sealed class PerformanceStatisticsService(
    AgeNexusDbContext database,
    IPerformanceCalculator calculator,
    IReplayStatisticsExtractor replayExtractor,
    IMatchWorkflowService matchWorkflow,
    IConfiguration configuration) : IPerformanceStatisticsService
{
    private const string ManualMvpRuleVersion = "2026.09-manual-mvp.1";

    public async Task<PerformanceReportView?> GetAsync(
        Guid matchId,
        CancellationToken cancellationToken = default)
    {
        var match = await LoadMatchAsync(matchId, cancellationToken);
        if (match is null)
        {
            return null;
        }

        var humans = match.Teams.SelectMany(team => team.Participants
            .Where(x => x.Type == ParticipantType.Human)
            .Select(x => new { Team = team, PlayerId = x.PlayerProfileId!.Value })).ToArray();
        var playerIds = humans.Select(x => x.PlayerId).ToArray();
        var names = await database.PlayerProfiles.AsNoTracking().Where(x => playerIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);
        var report = await database.MatchStatisticsReports.AsNoTracking()
            .SingleOrDefaultAsync(x => x.MatchId == matchId, cancellationToken);
        PlayerMatchStatistics[] statistics = report is null
            ? []
            : await database.PlayerMatchStatistics.AsNoTracking()
                .Where(x => x.ReportId == report.Id).ToArrayAsync(cancellationToken);
        PlayerPerformanceScore[] scores = report is null
            ? []
            : await database.PlayerPerformanceScores.AsNoTracking()
                .Where(x => x.ReportId == report.Id).ToArrayAsync(cancellationToken);
        Guid[] confirmedTeams = report is null
            ? []
            : await database.StatisticsConfirmations.AsNoTracking()
                .Where(x => x.ReportId == report.Id && x.Decision == StatisticsConfirmationDecision.Confirmed)
                .Select(x => x.TeamId).ToArrayAsync(cancellationToken);
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

        if (!CanManageMatch(match, request.SubmittedByPlayerProfileId))
        {
            return PerformanceOperationResult.Failure("PlayerNotInMatch");
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
                    request.Source, DateTimeOffset.UtcNow);
                database.MatchStatisticsReports.Add(report);
            }
            else if (report.Status != MatchStatisticsStatus.Draft)
            {
                return PerformanceOperationResult.Failure("ReportIsLocked");
            }

            var existing = await database.PlayerMatchStatistics
                .Where(x => x.ReportId == report.Id)
                .ToDictionaryAsync(x => x.PlayerProfileId, cancellationToken);
            foreach (var submitted in request.Players)
            {
                var origin = request.Source == MatchStatisticsSource.ScreenshotTranscription
                    ? StatisticValueOrigin.Screenshot
                    : submitted.Origin;
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

    public async Task<PerformanceOperationResult> ImportReplayAsync(
        ImportReplayRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var match = await LoadMatchAsync(request.MatchId, cancellationToken);
        if (match is null)
        {
            return PerformanceOperationResult.Failure("MatchNotFound");
        }

        if (!CanManageMatch(match, request.SubmittedByPlayerProfileId))
        {
            return PerformanceOperationResult.Failure("PlayerNotInMatch");
        }

        if (await database.MatchStatisticsReports.AnyAsync(x => x.MatchId == request.MatchId, cancellationToken))
        {
            return PerformanceOperationResult.Failure("ReportAlreadyExists");
        }

        var extraction = await replayExtractor.ExtractAsync(request.FileName, request.Content, cancellationToken);
        if (!extraction.Succeeded)
        {
            return PerformanceOperationResult.Failure(extraction.ErrorCode ?? "ReplayParseFailed");
        }

        var sha256 = Convert.ToHexString(SHA256.HashData(request.Content)).ToLowerInvariant();
        var report = new MatchStatisticsReport(
            Guid.NewGuid(), match.Id, request.SubmittedByPlayerProfileId, MatchStatisticsSource.Replay,
            DateTimeOffset.UtcNow, request.FileName, sha256, extraction.ExtractorVersion, extraction.CoverageDetails);
        database.MatchStatisticsReports.Add(report);
        var participantTeams = HumanParticipantTeams(match);
        var playerIds = participantTeams.Keys.ToArray();
        var playerNames = await database.PlayerProfiles.AsNoTracking().Where(x => playerIds.Contains(x.Id))
            .ToDictionaryAsync(x => NormalizeName(x.DisplayName), x => x.Id, cancellationToken);
        var warnings = new List<string>(extraction.Warnings ?? []);
        foreach (var extracted in extraction.Players.Where(x => x.IsHuman))
        {
            if (!playerNames.TryGetValue(NormalizeName(extracted.Name), out var playerId))
            {
                warnings.Add($"O jogador '{extracted.Name}' do replay não corresponde a um perfil da partida.");
                continue;
            }

            database.PlayerMatchStatistics.Add(new PlayerMatchStatistics(
                Guid.NewGuid(), report.Id, match.Id, participantTeams[playerId], playerId,
                StatisticValueOrigin.Extracted, extracted.Values));
        }

        await database.SaveChangesAsync(cancellationToken);
        return PerformanceOperationResult.Success(report.Id, warnings: warnings);
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
        if (match is null || !CanManageMatch(match, playerProfileId))
        {
            return PerformanceOperationResult.Failure("PlayerNotInMatch");
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
        if (match is null || !SingleAdministratorMode || match.CreatedByPlayerProfileId != administratorPlayerProfileId)
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

    private static Dictionary<Guid, Guid> HumanParticipantTeams(Match match) =>
        match.Teams.SelectMany(team => team.Participants
            .Where(x => x.Type == ParticipantType.Human)
            .Select(x => new { PlayerId = x.PlayerProfileId!.Value, TeamId = team.Id }))
            .ToDictionary(x => x.PlayerId, x => x.TeamId);

    private static bool IsHumanParticipant(Match match, Guid playerId) =>
        match.Teams.SelectMany(x => x.Participants)
            .Any(x => x.Type == ParticipantType.Human && x.PlayerProfileId == playerId);

    private bool CanManageMatch(Match match, Guid playerId) =>
        IsHumanParticipant(match, playerId) ||
        (SingleAdministratorMode && match.CreatedByPlayerProfileId == playerId);

    private bool SingleAdministratorMode =>
        configuration.GetValue("OperatingMode:SingleAdministrator", true);

    private static bool IsComplete(MatchStatisticValues values) =>
        values.UnitsKilled.HasValue && values.UnitsLost.HasValue && values.BuildingsDestroyed.HasValue &&
        values.BuildingsLost.HasValue && values.LargestArmy.HasValue && values.PeakVillagers.HasValue &&
        values.FoodCollected.HasValue && values.WoodCollected.HasValue && values.GoldCollected.HasValue &&
        values.StoneCollected.HasValue && values.MilitaryScore.HasValue && values.EconomyScore.HasValue &&
        values.TechnologyScore.HasValue && values.SocietyScore.HasValue && values.TotalScore.HasValue;

    private static string NormalizeName(string value)
    {
        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        return string.Concat(decomposed.Where(x => CharUnicodeInfo.GetUnicodeCategory(x) != UnicodeCategory.NonSpacingMark))
            .Normalize(NormalizationForm.FormC).ToUpperInvariant();
    }
}
