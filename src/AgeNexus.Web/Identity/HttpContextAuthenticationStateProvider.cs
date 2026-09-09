using System.Security.Claims;
using AgeNexus.Infrastructure.Identity;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AgeNexus.Web.Identity;

// ServerAuthenticationStateProvider receives the authenticated principal for SSR and circuits.
// Revalidation also invalidates an open circuit after role removal, lockout or stamp rotation.
public sealed class HttpContextAuthenticationStateProvider(
    ILoggerFactory loggerFactory,
    IServiceScopeFactory scopeFactory,
    IOptions<IdentityOptions> identityOptions)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(5);

    protected override Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState, CancellationToken cancellationToken) =>
        IsSessionValidAsync(authenticationState.User, cancellationToken);

    public async Task<bool> IsSessionValidAsync(
        ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.GetUserAsync(principal);
        cancellationToken.ThrowIfCancellationRequested();
        if (user is null || (users.SupportsUserLockout && await users.IsLockedOutAsync(user)))
        {
            return false;
        }

        var claims = identityOptions.Value.ClaimsIdentity;
        if (users.SupportsUserSecurityStamp &&
            principal.FindFirstValue(claims.SecurityStampClaimType) != await users.GetSecurityStampAsync(user))
        {
            return false;
        }

        var currentRoles = await users.GetRolesAsync(user);
        return new HashSet<string>(principal.FindAll(claims.RoleClaimType).Select(x => x.Value),
            StringComparer.Ordinal).SetEquals(currentRoles);
    }
}
