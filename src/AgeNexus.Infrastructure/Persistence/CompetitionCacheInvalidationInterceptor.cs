using AgeNexus.Infrastructure.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AgeNexus.Infrastructure.Persistence;

internal sealed class CompetitionCacheInvalidationInterceptor(CompetitionQueryCache cache)
    : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        InvalidateForDomainChanges(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        InvalidateForDomainChanges(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void InvalidateForDomainChanges(DbContext? context)
    {
        if (context?.ChangeTracker.Entries().Any(entry =>
                (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted) &&
                entry.Entity.GetType().Assembly == typeof(Domain.Matches.Match).Assembly) == true)
        {
            cache.Invalidate();
        }
    }
}
