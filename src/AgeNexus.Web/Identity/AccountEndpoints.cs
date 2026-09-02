using AgeNexus.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Antiforgery;

namespace AgeNexus.Web.Identity;

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAgeNexusAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/account");
        group.MapPost("/register", RegisterAsync);
        group.MapPost("/login", LoginAsync);
        group.MapPost("/external/google", BeginGoogleLoginAsync);
        group.MapGet("/external/google/callback", CompleteGoogleLoginAsync);
        group.MapPost("/logout", LogoutAsync).RequireAuthorization();
        group.MapPost("/profile", UpdateProfileAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        AccountService accounts,
        CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestAsync(context);
        var form = await context.Request.ReadFormAsync(cancellationToken);
        if (!string.Equals(
                form["password"].ToString(),
                form["passwordConfirmation"].ToString(),
                StringComparison.Ordinal))
        {
            return Results.LocalRedirect("/conta/criar?erro=senhas-diferentes");
        }

        var result = await accounts.RegisterAsync(
            form["email"].ToString(),
            form["password"].ToString(),
            form["displayName"].ToString(),
            cancellationToken);

        if (result.Succeeded)
        {
            return Results.LocalRedirect(GetSafeReturnUrl(form["returnUrl"].ToString()));
        }

        var error = result.ErrorCodes.Any(x => x.Contains("Duplicate", StringComparison.OrdinalIgnoreCase))
            ? "email-em-uso"
            : result.ErrorCodes.Any(x => x.Contains("Password", StringComparison.OrdinalIgnoreCase))
                ? "senha-fraca"
                : "cadastro-invalido";
        return Results.LocalRedirect($"/conta/criar?erro={error}");
    }

    private static async Task<IResult> LoginAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        AccountService accounts)
    {
        await antiforgery.ValidateRequestAsync(context);
        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        var result = await accounts.LoginAsync(
            form["email"].ToString(),
            form["password"].ToString(),
            string.Equals(form["rememberMe"].ToString(), "true", StringComparison.OrdinalIgnoreCase));

        if (result.Succeeded)
        {
            return Results.LocalRedirect(GetSafeReturnUrl(form["returnUrl"].ToString()));
        }

        var error = result.ErrorCodes.Contains("LockedOut") ? "bloqueado" : "credenciais";
        return Results.LocalRedirect($"/conta/login?erro={error}");
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
            return Results.LocalRedirect(GetSafeReturnUrl(context.Request.Query["returnUrl"].ToString()));
        }

        var error = result.ErrorCodes.Contains("LockedOut")
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
        var result = await accounts.UpdateProfileAsync(
            context.User,
            form["displayName"].ToString(),
            form["bio"].ToString(),
            form["location"].ToString(),
            form["avatarUrl"].ToString(),
            cancellationToken);

        return Results.LocalRedirect(result.Succeeded ? "/perfil?salvo=1" : "/perfil?erro=perfil-invalido");
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
}
