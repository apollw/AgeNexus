using AgeNexus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AgeNexus.Web.Clans;

public static class ClanEndpoints
{
    public static IEndpointRouteBuilder MapAgeNexusClanEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/clan-images/{clanId:guid}", GetClanImageAsync);
        return endpoints;
    }

    private static async Task<IResult> GetClanImageAsync(
        Guid clanId,
        HttpContext context,
        AgeNexusDbContext database,
        IMemoryCache cache,
        CancellationToken cancellationToken)
    {
        var requestedVersion = context.Request.Query["v"].ToString();
        var hasVersion = requestedVersion.Length == 12 && requestedVersion.All(Uri.IsHexDigit);
        var cacheKey = $"clan-emblem:{clanId:D}:{requestedVersion}";
        ClanEmblemResponse? emblem = null;
        if (hasVersion) cache.TryGetValue(cacheKey, out emblem);

        emblem ??= await database.ClanEmblems.AsNoTracking()
            .Where(x => x.ClanId == clanId)
            .Select(x => new ClanEmblemResponse(x.ContentType, x.Content, x.Sha256))
            .SingleOrDefaultAsync(cancellationToken);
        if (emblem is null || hasVersion &&
            !emblem.Sha256.StartsWith(requestedVersion, StringComparison.OrdinalIgnoreCase))
        {
            return Results.NotFound();
        }

        if (hasVersion)
        {
            cache.Set(cacheKey, emblem, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6),
                Size = Math.Max(1, (int)Math.Ceiling(emblem.Content.Length / (64d * 1024d)))
            });
            context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
        }
        else
        {
            context.Response.Headers.CacheControl = "public,max-age=300";
        }

        var etag = $"\"{emblem.Sha256}\"";
        context.Response.Headers.ETag = etag;
        if (context.Request.Headers.IfNoneMatch.Any(value => value == etag))
            return Results.StatusCode(StatusCodes.Status304NotModified);

        return Results.Bytes(emblem.Content, emblem.ContentType);
    }

    private sealed record ClanEmblemResponse(string ContentType, byte[] Content, string Sha256);
}
