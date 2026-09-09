using System.Security.Claims;
using AgeNexus.Application.Evidence;
using AgeNexus.Domain.Players;
using AgeNexus.Infrastructure.Identity;
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
        services.AddScoped<AccountService>();
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
