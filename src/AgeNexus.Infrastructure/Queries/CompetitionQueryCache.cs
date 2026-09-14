using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace AgeNexus.Infrastructure.Queries;

internal class CompetitionQueryCache(IMemoryCache cache, ILogger<CompetitionQueryCache> logger,
    string cacheNamespace = "competition")
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);
    private readonly SemaphoreSlim[] gates = Enumerable.Range(0, 32)
        .Select(_ => new SemaphoreSlim(1, 1))
        .ToArray();
    private long version;

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken)
        where T : notnull
    {
        var currentVersion = Volatile.Read(ref version);
        var versionedKey = $"{cacheNamespace}:{currentVersion}:{key}";
        if (cache.TryGetValue(versionedKey, out T? cached) && cached is not null)
        {
            return cached;
        }

        var gateIndex = (int)((uint)StringComparer.Ordinal.GetHashCode(versionedKey) % (uint)gates.Length);
        var gate = gates[gateIndex];
        var elapsed = Stopwatch.StartNew();
        await gate.WaitAsync(cancellationToken);
        var waitMilliseconds = elapsed.ElapsedMilliseconds;
        try
        {
            if (cache.TryGetValue(versionedKey, out cached) && cached is not null)
            {
                return cached;
            }

            var value = await factory(cancellationToken);
            if (currentVersion == Volatile.Read(ref version))
            {
                cache.Set(versionedKey, value, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = Lifetime,
                    Size = 1
                });
            }

            return value;
        }
        finally
        {
            gate.Release();
            if (elapsed.ElapsedMilliseconds >= 500)
            {
                logger.LogWarning("Slow query group {QueryGroup}: {ElapsedMs}ms total, {WaitMs}ms waiting for cache. Trace {TraceId}",
                    key.Split(':')[0], elapsed.ElapsedMilliseconds, waitMilliseconds, Activity.Current?.TraceId.ToString());
            }
        }
    }

    public void Invalidate() => Interlocked.Increment(ref version);
}
