using AgeNexus.Infrastructure.Identity;
using AgeNexus.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AgeNexus.Web.Identity;

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAgeNexusAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/account");
        group.MapPost("/external/google", BeginGoogleLoginAsync);
        group.MapGet("/external/google/callback", CompleteGoogleLoginAsync);
        group.MapPost("/logout", LogoutAsync).RequireAuthorization();
        group.MapPost("/profile", UpdateProfileAsync).RequireAuthorization();
        endpoints.MapGet("/profile-images/{playerProfileId:guid}", GetProfileImageAsync);
        return endpoints;
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        AccountService accounts)
    {
        await antiforgery.ValidateRequestAsync(context);
        await accounts.LogoutAsync();
        return Results.LocalRedirect("/");
    }

    private static async Task<IResult> BeginGoogleLoginAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        IAuthenticationSchemeProvider schemes,
        AccountService accounts)
    {
        await antiforgery.ValidateRequestAsync(context);
        if (await schemes.GetSchemeAsync(AccountService.GoogleProvider) is null)
        {
            return Results.LocalRedirect("/conta/login?erro=google-indisponivel");
        }

        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        var returnUrl = GetSafeReturnUrl(form["returnUrl"].ToString());
        var callbackUrl = "/account/external/google/callback" +
                          QueryString.Create("returnUrl", returnUrl).ToUriComponent();
        var properties = accounts.ConfigureExternalLogin(callbackUrl);
        return Results.Challenge(properties, [AccountService.GoogleProvider]);
    }

    private static async Task<IResult> CompleteGoogleLoginAsync(
        HttpContext context,
        AccountService accounts,
        CancellationToken cancellationToken)
    {
        var result = await accounts.CompleteGoogleLoginAsync(cancellationToken);
        if (result.Succeeded)
        {
            if (result.RequiresProfileSetup)
            {
                return Results.LocalRedirect("/conta/vincular-perfil");
            }
            return Results.LocalRedirect(GetSafeReturnUrl(context.Request.Query["returnUrl"].ToString()));
        }

        var error = result.ErrorCodes.Contains("RegistrationClosed")
            ? "acesso-restrito"
            : result.ErrorCodes.Contains("LockedOut")
            ? "bloqueado"
            : result.ErrorCodes.Contains("ExternalAccountLinkRequired")
                ? "google-vinculacao"
                : "google";
        return Results.LocalRedirect($"/conta/login?erro={error}");
    }

    private static async Task<IResult> UpdateProfileAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        AccountService accounts,
        CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestAsync(context);
        var form = await context.Request.ReadFormAsync(cancellationToken);
        var favoriteFactionId = Guid.TryParse(form["favoriteFactionId"].ToString(), out var parsedFactionId)
            ? parsedFactionId
            : (Guid?)null;
        var result = await accounts.UpdateProfileAsync(
            context.User,
            form["displayName"].ToString(),
            form["bio"].ToString(),
            form["location"].ToString(),
            favoriteFactionId,
            cancellationToken);

        var error = result.ErrorCodes.Contains("DuplicateDisplayName") ? "nick-em-uso" : "perfil-invalido";
        return Results.LocalRedirect(result.Succeeded ? "/perfil?salvo=1" : $"/perfil?erro={error}");
    }

    private static string GetSafeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) ||
            !returnUrl.StartsWith('/') ||
            returnUrl.StartsWith("//", StringComparison.Ordinal) ||
            returnUrl.StartsWith("/\\", StringComparison.Ordinal))
        {
            return "/";
        }

        return returnUrl;
    }

    private static async Task<IResult> GetProfileImageAsync(
        Guid playerProfileId,
        HttpContext context,
        AgeNexusDbContext database,
        IMemoryCache cache,
        CancellationToken cancellationToken)
    {
        var requestedVersion = context.Request.Query["v"].ToString();
        var hasVersion = requestedVersion.Length == 12 && requestedVersion.All(Uri.IsHexDigit);
        var cacheKey = $"profile-avatar:{playerProfileId:D}:{requestedVersion}";
        ProfileAvatarResponse? avatar = null;
        if (hasVersion)
        {
            cache.TryGetValue(cacheKey, out avatar);
        }

        avatar ??= await database.PlayerProfileAvatars.AsNoTracking()
            .Where(x => x.PlayerProfileId == playerProfileId)
            .Select(x => new ProfileAvatarResponse(x.ContentType, x.Content, x.Sha256))
            .SingleOrDefaultAsync(cancellationToken);
        if (avatar is null)
        {
            return Results.NotFound();
        }

        if (hasVersion && !avatar.Sha256.StartsWith(requestedVersion, StringComparison.OrdinalIgnoreCase))
        {
            return Results.NotFound();
        }

        if (hasVersion)
        {
            cache.Set(cacheKey, avatar, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6),
                Size = Math.Max(1, (int)Math.Ceiling(avatar.Content.Length / (64d * 1024d)))
            });
            context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
        }
        else
        {
            context.Response.Headers.CacheControl = "public,max-age=300";
        }

        var etag = $"\"{avatar.Sha256}\"";
        context.Response.Headers.ETag = etag;
        if (context.Request.Headers.IfNoneMatch.Any(value => value == etag))
        {
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        return Results.Bytes(avatar.Content, avatar.ContentType);
    }

    private sealed record ProfileAvatarResponse(string ContentType, byte[] Content, string Sha256);
}
