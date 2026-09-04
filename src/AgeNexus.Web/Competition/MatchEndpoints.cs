using AgeNexus.Application.Matches;
using AgeNexus.Infrastructure.Identity;
using Microsoft.AspNetCore.Antiforgery;

namespace AgeNexus.Web.Competition;

public static class MatchEndpoints
{
    public static IEndpointRouteBuilder MapAgeNexusMatchEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/matches/delete", DeleteAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> DeleteAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        AccountService accounts,
        IMatchWorkflowService workflow,
        CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestAsync(context);
        var form = await context.Request.ReadFormAsync(cancellationToken);
        if (!Guid.TryParse(form["matchId"].ToString(), out var matchId))
        {
            return Redirect("invalid");
        }

        var profile = await accounts.GetProfileAsync(context.User, cancellationToken);
        if (profile is null)
        {
            return Redirect("profile");
        }

        var result = await workflow.DeleteAsync(matchId, profile.Id, cancellationToken);
        return Redirect(result.Succeeded ? "ok" : result.ErrorCode ?? "error");
    }

    private static IResult Redirect(string status) =>
        Results.LocalRedirect("/partidas" + QueryString.Create("exclusao", status));
}
