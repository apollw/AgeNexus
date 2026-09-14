using AgeNexus.Domain.Matches;
using AgeNexus.Domain.Players;
using AgeNexus.Infrastructure.Persistence;
using AgeNexus.Infrastructure.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgeNexus.Domain.Tests;

public sealed class QueryCacheTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Saving_matches_preserves_catalog_but_player_changes_refresh_it(bool asynchronous)
    {
        using var memory = new MemoryCache(new MemoryCacheOptions { SizeLimit = 512 });
        var logger = NullLogger<CompetitionQueryCache>.Instance;
        var competition = new CompetitionQueryCache(memory, logger);
        var catalog = new CatalogQueryCache(memory, logger);
        var options = new DbContextOptionsBuilder<AgeNexusDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new CompetitionCacheInvalidationInterceptor(competition, catalog)).Options;
        await using var database = new AgeNexusDbContext(options);
        var player = new PlayerProfile(Guid.NewGuid(), "Original");
        database.Add(player);
        await database.SaveChangesAsync();
        async Task<string> ReadCatalog() => await catalog.GetOrCreateAsync("same-key",
            _ => Task.FromResult(player.DisplayName), default);
        Assert.Equal("Original", await ReadCatalog());
        Assert.Equal("history", await competition.GetOrCreateAsync("same-key",
            _ => Task.FromResult("history"), default));
        for (var i = 0; i < 8; i++)
        {
            database.Add(new Match(Guid.NewGuid(), Guid.NewGuid(), player.Id,
                DateTimeOffset.UtcNow, default, default));
            if (asynchronous) await database.SaveChangesAsync(); else database.SaveChanges();
            Assert.Equal("Original", await catalog.GetOrCreateAsync<string>("same-key",
                _ => throw new InvalidOperationException("Match save evicted the catalog"), default));
            Assert.Equal(i, await competition.GetOrCreateAsync("same-key", _ => Task.FromResult(i), default));
        }
        player.UpdatePublicProfile("Renamed", null, null, null);
        if (asynchronous) await database.SaveChangesAsync(); else database.SaveChanges();
        Assert.Equal("Renamed", await ReadCatalog());
    }
}
