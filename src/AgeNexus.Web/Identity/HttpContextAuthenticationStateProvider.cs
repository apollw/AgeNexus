using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace AgeNexus.Web.Identity;

public sealed class HttpContextAuthenticationStateProvider(IHttpContextAccessor httpContextAccessor)
    : AuthenticationStateProvider
{
    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var principal = httpContextAccessor.HttpContext?.User ??
                        new ClaimsPrincipal(new ClaimsIdentity());
        return Task.FromResult(new AuthenticationState(principal));
    }
}
