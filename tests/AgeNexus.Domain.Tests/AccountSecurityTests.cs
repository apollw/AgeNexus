using System.Security.Claims;
using AgeNexus.Application;
using AgeNexus.Application.Evidence;
using AgeNexus.Application.MatchPerformance;
using AgeNexus.Application.Matches;
using AgeNexus.Application.Queries;
using AgeNexus.Domain.Competition;
using AgeNexus.Domain.MatchPerformance;
using AgeNexus.Domain.Matches;
using AgeNexus.Domain.Players;
using AgeNexus.Infrastructure.Competition;
using AgeNexus.Infrastructure.Clans;
using AgeNexus.Infrastructure.Identity;
using AgeNexus.Infrastructure.MatchPerformance;
using AgeNexus.Infrastructure.Persistence;
using AgeNexus.Web.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgeNexus.Domain.Tests;

public sealed class AccountSecurityTests
{
    [Fact]
    public async Task First_registration_does_not_grant_admin_without_explicit_configuration()
    {
        await using var services = CreateServices();
        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await AddUserAsync(users, "first@example.test");
        await scope.ServiceProvider.GetRequiredService<AccountService>().EnsureAdministratorRoleAsync();
        Assert.False(await users.IsInRoleAsync(user, AccountService.AdministratorRole));
    }

    [Fact]
    public async Task Bootstrap_grants_only_the_configured_verified_account()
    {
        await using var services = CreateServices("owner@example.test");
        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var first = await AddUserAsync(users, "first@example.test");
        var owner = await AddUserAsync(users, "owner@example.test", verified: false);
        var accounts = scope.ServiceProvider.GetRequiredService<AccountService>();
        await accounts.EnsureAdministratorRoleAsync();
        Assert.False(await users.IsInRoleAsync(first, AccountService.AdministratorRole));
        Assert.False(await users.IsInRoleAsync(owner, AccountService.AdministratorRole));
        owner.ConfirmEmailFromTrustedProvider();
        Assert.True((await users.UpdateAsync(owner)).Succeeded);
        await accounts.EnsureAdministratorRoleAsync();
        Assert.True(await users.IsInRoleAsync(owner, AccountService.AdministratorRole));
        Assert.False(await users.IsInRoleAsync(first, AccountService.AdministratorRole));
    }

    [Fact]
    public async Task Existing_administrator_is_preserved_without_bootstrap_configuration()
    {
        await using var services = CreateServices();
        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await AddUserAsync(users, "owner@example.test");
        var accounts = scope.ServiceProvider.GetRequiredService<AccountService>();
        await accounts.EnsureAdministratorRoleAsync();
        Assert.True((await users.AddToRoleAsync(user, AccountService.AdministratorRole)).Succeeded);
        await accounts.EnsureAdministratorRoleAsync();
        Assert.True(await users.IsInRoleAsync(user, AccountService.AdministratorRole));
    }

    [Fact]
    public async Task Statistics_submission_requires_an_active_linked_profile()
    {
        await using var services = CreateServices();
        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await AddUserAsync(users, "player@example.test");
        var database = scope.ServiceProvider.GetRequiredService<AgeNexusDbContext>();
        var linked = new PlayerProfile(Guid.NewGuid(), "Linked", user.Id);
        var historical = new PlayerProfile(Guid.NewGuid(), "Historical");
        database.PlayerProfiles.AddRange(linked, historical);
        await database.SaveChangesAsync();
        var accounts = scope.ServiceProvider.GetRequiredService<AccountService>();

        Assert.True(await accounts.IsActiveLinkedProfileAsync(linked.Id));
        Assert.False(await accounts.IsActiveLinkedProfileAsync(historical.Id));

        Assert.True((await users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddHours(1))).Succeeded);
        Assert.False(await accounts.IsActiveLinkedProfileAsync(linked.Id));
    }

    [Fact]
    public async Task Linked_player_can_create_a_clan_with_an_initial_roster()
    {
        await using var services = CreateServices();
        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var founderUser = await AddUserAsync(users, "founder@example.test");
        var database = scope.ServiceProvider.GetRequiredService<AgeNexusDbContext>();
        var founder = new PlayerProfile(Guid.NewGuid(), "Fundador", founderUser.Id);
        var member = new PlayerProfile(Guid.NewGuid(), "Integrante");
        var unlinkedCreator = new PlayerProfile(Guid.NewGuid(), "Sem conta");
        database.PlayerProfiles.AddRange(founder, member, unlinkedCreator);
        await database.SaveChangesAsync();
        var clans = scope.ServiceProvider.GetRequiredService<IClanService>();

        var created = await clans.CreateAsync(founder.Id, "Guardiões", "GDN", [member.Id]);

        Assert.True(created.Succeeded);
        Assert.NotNull(created.ClanId);
        Assert.Equal(2, await database.ClanMemberships.CountAsync(x => x.ClanId == created.ClanId!.Value));
        Assert.False((await clans.CreateAsync(unlinkedCreator.Id, "Invasores", "INV", [])).Succeeded);
        Assert.False((await clans.CreateAsync(founder.Id, "Outro", "OUT", [])).Succeeded);
    }

    [Fact]
    public async Task Claim_requires_admin_and_profile_changes_cannot_target_another_player()
    {
        await using var services = CreateServices("owner@example.test");
        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var owner = await AddUserAsync(users, "owner@example.test");
        var claimant = await AddUserAsync(users, "player@example.test");
        var accounts = scope.ServiceProvider.GetRequiredService<AccountService>();
        await accounts.EnsureAdministratorRoleAsync();
        var database = scope.ServiceProvider.GetRequiredService<AgeNexusDbContext>();
        var historical = new PlayerProfile(Guid.NewGuid(), "Historical");
        var other = new PlayerProfile(Guid.NewGuid(), "Other", owner.Id);
        database.PlayerProfiles.AddRange(historical, other);
        await database.SaveChangesAsync();
        var playerPrincipal = Principal(claimant);
        Assert.True((await accounts.RequestProfileClaimAsync(playerPrincipal, historical.Id)).Succeeded);
        Assert.Null(historical.ApplicationUserId);
        var claim = await database.PlayerProfileClaims.SingleAsync();
        // Even a principal carrying a forged role claim is checked against the database.
        var forgedRole = Principal(claimant, AccountService.AdministratorRole);
        Assert.False((await accounts.DecideProfileClaimAsync(forgedRole, claim.Id, true)).Succeeded);
        Assert.True((await accounts.DecideProfileClaimAsync(Principal(owner), claim.Id, true)).Succeeded);
        Assert.Equal(claimant.Id, historical.ApplicationUserId);
        Assert.True((await accounts.UpdateProfileAsync(playerPrincipal, "Mine", null, null, null,
            CancellationToken.None)).Succeeded);
        Assert.Equal("Other", other.DisplayName);
        var anonymousWithId = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, claimant.Id.ToString())]));
        Assert.False((await accounts.UpdateProfileAsync(anonymousWithId, "Attack", null, null, null,
            CancellationToken.None)).Succeeded);
    }

    [Fact]
    public async Task Administrator_can_merge_history_without_overwriting_the_linked_profile_identity()
    {
        await using var services = CreateServices("owner@example.test");
        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var owner = await AddUserAsync(users, "owner@example.test");
        var player = await AddUserAsync(users, "player@example.test");
        var accounts = scope.ServiceProvider.GetRequiredService<AccountService>();
        await accounts.EnsureAdministratorRoleAsync();
        var database = scope.ServiceProvider.GetRequiredService<AgeNexusDbContext>();
        var historical = new PlayerProfile(Guid.NewGuid(), "Nome antigo");
        historical.UpdatePublicProfile("Nome antigo", "Bio antiga", "Local antigo", "https://example.test/old.png");
        var linked = new PlayerProfile(Guid.NewGuid(), "Nome novo", player.Id);
        linked.UpdatePublicProfile("Nome novo", "Bio nova", "Local novo", "https://example.test/new.png");
        var opponent = new PlayerProfile(Guid.NewGuid(), "Adversário");
        database.PlayerProfiles.AddRange(historical, linked, opponent);

        var match = new Match(Guid.NewGuid(), Guid.NewGuid(), historical.Id, DateTimeOffset.UtcNow,
            MatchType.PlayerVersusPlayer, MatchNature.Ranked);
        var firstTeam = match.AddTeam(Guid.NewGuid());
        var secondTeam = match.AddTeam(Guid.NewGuid());
        match.AddParticipant(firstTeam.Id, MatchParticipant.Human(Guid.NewGuid(), historical.Id));
        match.AddParticipant(secondTeam.Id, MatchParticipant.Human(Guid.NewGuid(), opponent.Id));
        database.Matches.Add(match);
        database.RatingEvents.Add(new RatingEvent(Guid.NewGuid(), match.Id, historical.Id, null,
            RatingScopeKind.GeneralCompetitive, 18m, "test-v1", "{}", DateTimeOffset.UtcNow));
        database.PointEvents.Add(new PointEvent(Guid.NewGuid(), match.Id, historical.Id, null,
            PointScopeKind.Career, 25m, "test-v1", "{}", DateTimeOffset.UtcNow));
        await database.SaveChangesAsync();

        var result = await accounts.MergeHistoricalProfileAsync(Principal(owner), historical.Id, linked.Id);

        Assert.True(result.Succeeded);
        Assert.False(await database.PlayerProfiles.AnyAsync(x => x.Id == historical.Id));
        var preserved = await database.PlayerProfiles.SingleAsync(x => x.Id == linked.Id);
        Assert.Equal("Nome novo", preserved.DisplayName);
        Assert.Equal("Bio nova", preserved.Bio);
        Assert.Equal("Local novo", preserved.Location);
        Assert.Equal("https://example.test/new.png", preserved.AvatarUrl);
        Assert.Equal(linked.Id, await database.MatchParticipants.Where(x => x.Id == firstTeam.Participants.Single().Id)
            .Select(x => x.PlayerProfileId).SingleAsync());
        Assert.Equal(linked.Id, await database.RatingEvents.Select(x => x.BeneficiaryId).SingleAsync());
        Assert.Equal(linked.Id, await database.PointEvents.Select(x => x.BeneficiaryId).SingleAsync());
        Assert.Equal(linked.Id, await database.Matches.Select(x => x.CreatedByPlayerProfileId).SingleAsync());
    }

    [Fact]
    public async Task Active_linked_player_can_register_a_match_without_administrator_role()
    {
        await using var services = CreateServices();
        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await AddUserAsync(users, "player@example.test");
        var database = scope.ServiceProvider.GetRequiredService<AgeNexusDbContext>();
        var creator = new PlayerProfile(Guid.NewGuid(), "Jogador", user.Id);
        var opponent = new PlayerProfile(Guid.NewGuid(), "Adversário");
        database.PlayerProfiles.AddRange(creator, opponent);
        await database.SaveChangesAsync();
        var workflow = scope.ServiceProvider.GetRequiredService<IMatchWorkflowService>();

        var result = await workflow.RegisterAsync(new RegisterMatchRequest(
            Guid.NewGuid(), creator.Id, DateTimeOffset.UtcNow, MatchType.PlayerVersusPlayer,
            MatchNature.Ranked, MatchGameMode.Standard, null, null, null,
            [
                new RegisterMatchTeam(TeamResult.Victory,
                    [new RegisterMatchParticipant(ParticipantType.Human, creator.Id, null, null, FactionSelection.Unknown)]),
                new RegisterMatchTeam(TeamResult.Defeat,
                    [new RegisterMatchParticipant(ParticipantType.Human, opponent.Id, null, null, FactionSelection.Unknown)])
            ]));

        Assert.True(result.Succeeded);
        var match = await database.Matches.SingleAsync(x => x.Id == result.MatchId);
        Assert.Equal(creator.Id, match.CreatedByPlayerProfileId);
        Assert.Equal(MatchStatus.AwaitingConfirmation, match.Status);
        Assert.False(await users.IsInRoleAsync(user, AccountService.AdministratorRole));
    }

    [Fact]
    public async Task Match_creator_can_resume_and_submit_a_saved_draft()
    {
        await using var services = CreateServices();
        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var creatorUser = await AddUserAsync(users, "creator@example.test");
        var draftUser = await AddUserAsync(users, "draft@example.test");
        var otherUser = await AddUserAsync(users, "other@example.test");
        var database = scope.ServiceProvider.GetRequiredService<AgeNexusDbContext>();
        var creator = new PlayerProfile(Guid.NewGuid(), "Criador", creatorUser.Id);
        var opponent = new PlayerProfile(Guid.NewGuid(), "Adversário", draftUser.Id);
        var other = new PlayerProfile(Guid.NewGuid(), "Outro", otherUser.Id);
        database.PlayerProfiles.AddRange(creator, opponent, other);
        await database.SaveChangesAsync();
        var workflow = scope.ServiceProvider.GetRequiredService<IMatchWorkflowService>();
        var registration = await workflow.RegisterAsync(new RegisterMatchRequest(
            Guid.NewGuid(), creator.Id, DateTimeOffset.UtcNow, MatchType.PlayerVersusPlayer,
            MatchNature.Ranked, MatchGameMode.Standard, null, null, null,
            [
                new RegisterMatchTeam(TeamResult.Victory,
                    [new RegisterMatchParticipant(ParticipantType.Human, creator.Id, null, null, FactionSelection.Unknown)]),
                new RegisterMatchTeam(TeamResult.Defeat,
                    [new RegisterMatchParticipant(ParticipantType.Human, opponent.Id, null, null, FactionSelection.Unknown)])
            ]));
        Assert.True(registration.Succeeded);
        var match = await database.Matches.Include(x => x.Teams).ThenInclude(x => x.Participants)
            .SingleAsync(x => x.Id == registration.MatchId);
        var report = new MatchStatisticsReport(
            Guid.NewGuid(), match.Id, opponent.Id, MatchStatisticsSource.Manual, DateTimeOffset.UtcNow);
        database.MatchStatisticsReports.Add(report);
        await database.SaveChangesAsync();
        var participants = match.Teams.SelectMany(x => x.Participants).ToArray();
        var completeValues = new MatchStatisticValues(1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1);
        var performance = scope.ServiceProvider.GetRequiredService<IPerformanceStatisticsService>();
        var submittedRows = participants
            .Select(x => new SavePlayerStatistics(x.Id, completeValues)).ToArray();

        var blocked = await performance.SaveManualAsync(new SavePerformanceReportRequest(
            match.Id, other.Id, MatchStatisticsSource.Manual, submittedRows));
        var save = await performance.SaveManualAsync(new SavePerformanceReportRequest(
            match.Id, creator.Id, MatchStatisticsSource.Manual, submittedRows));
        var submit = await performance.SubmitAsync(report.Id, creator.Id);

        Assert.False(blocked.Succeeded);
        Assert.Equal("ReportOwnedByAnotherUser", blocked.ErrorCode);
        Assert.True(save.Succeeded);
        Assert.True(submit.Succeeded);
        var submitted = await database.MatchStatisticsReports.SingleAsync(x => x.Id == report.Id);
        Assert.Equal(MatchStatisticsStatus.Submitted, submitted.Status);
        Assert.Equal(creator.Id, submitted.SubmittedByPlayerProfileId);
    }

    [Theory]
    [InlineData("stamp")]
    [InlineData("role")]
    [InlineData("lockout")]
    [InlineData("deleted")]
    public async Task Open_session_is_invalidated_when_account_access_changes(string change)
    {
        await using var services = CreateServices("owner@example.test");
        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await AddUserAsync(users, "owner@example.test");
        await scope.ServiceProvider.GetRequiredService<AccountService>().EnsureAdministratorRoleAsync();
        var factory = scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>();
        var principal = await factory.CreateAsync(user);
        using var provider = ActivatorUtilities.CreateInstance<HttpContextAuthenticationStateProvider>(services);
        Assert.True(await provider.IsSessionValidAsync(principal));
        var result = change switch
        {
            "stamp" => await users.UpdateSecurityStampAsync(user),
            "role" => await users.RemoveFromRoleAsync(user, AccountService.AdministratorRole),
            "lockout" => await users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddHours(1)),
            _ => await users.DeleteAsync(user)
        };
        Assert.True(result.Succeeded);
        Assert.False(await provider.IsSessionValidAsync(principal));
    }

    private static ServiceProvider CreateServices(string? bootstrapEmail = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddDataProtection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Security:BootstrapAdministratorEmail"] = bootstrapEmail }).Build());
        var databaseName = Guid.NewGuid().ToString();
        services.AddDbContext<AgeNexusDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AgeNexusDbContext>().AddSignInManager();
        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        services.AddSingleton<IEvidenceObjectStorage, DisabledStorage>();
        services.AddAgeNexusApplication();
        services.AddScoped<AccountService>();
        services.AddScoped<IMatchWorkflowService, MatchWorkflowService>();
        services.AddScoped<IClanService, ClanService>();
        services.AddScoped<AgeNexusDbContextFactory>();
        services.AddScoped<IPerformanceStatisticsService, PerformanceStatisticsService>();
        return services.BuildServiceProvider();
    }

    private static async Task<ApplicationUser> AddUserAsync(
        UserManager<ApplicationUser> users, string email, bool verified = true)
    {
        var user = new ApplicationUser(email);
        if (verified) user.ConfirmEmailFromTrustedProvider();
        Assert.True((await users.CreateAsync(user)).Succeeded);
        return user;
    }

    private static ClaimsPrincipal Principal(ApplicationUser user, string? role = null)
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())], "test");
        if (role is not null) identity.AddClaim(new Claim(ClaimTypes.Role, role));
        return new ClaimsPrincipal(identity);
    }

    private sealed class DisabledStorage : IEvidenceObjectStorage
    {
        public bool IsConfigured => false;
        public Task UploadAsync(string key, string type, byte[] content, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public string GetPublicUrl(string key) => throw new NotSupportedException();
        public string GetPublicDownloadUrl(string key, string file) => throw new NotSupportedException();
        public string? TryGetObjectKey(string url) => null;
    }
}
