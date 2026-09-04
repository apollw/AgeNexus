using Microsoft.Extensions.Caching.Memory;

namespace AgeNexus.Infrastructure.Queries;

internal sealed class CompetitionQueryCache(IMemoryCache cache)
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
        var versionedKey = $"competition:{currentVersion}:{key}";
        if (cache.TryGetValue(versionedKey, out T? cached) && cached is not null)
        {
            return cached;
        }

        var gateIndex = (int)((uint)StringComparer.Ordinal.GetHashCode(versionedKey) % (uint)gates.Length);
        var gate = gates[gateIndex];
        await gate.WaitAsync(cancellationToken);
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
        }
    }

    public void Invalidate() => Interlocked.Increment(ref version);
}
