namespace AgeNexus.Application.Statistics;

public interface IProjectionRebuilder
{
    Task RebuildAsync(RebuildScope scope, CancellationToken cancellationToken = default);
}

public sealed record RebuildScope(Guid? GameEditionId = null, Guid? SeasonId = null);
