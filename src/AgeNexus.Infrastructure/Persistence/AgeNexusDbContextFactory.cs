using Microsoft.EntityFrameworkCore;

namespace AgeNexus.Infrastructure.Persistence;

public sealed class AgeNexusDbContextFactory(DbContextOptions<AgeNexusDbContext> options)
{
    public AgeNexusDbContext CreateDbContext() => new(options);
}
