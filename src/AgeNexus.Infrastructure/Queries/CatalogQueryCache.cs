using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace AgeNexus.Infrastructure.Queries;

// Match/statistics writes must not discard edition, player and map dropdowns.
internal sealed class CatalogQueryCache(IMemoryCache cache, ILogger<CompetitionQueryCache> logger)
    : CompetitionQueryCache(cache, logger, "catalog");
