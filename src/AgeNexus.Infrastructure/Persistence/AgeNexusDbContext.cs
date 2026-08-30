using AgeNexus.Domain.GameCatalog;
using AgeNexus.Domain.Matches;
using AgeNexus.Domain.Players;
using Microsoft.EntityFrameworkCore;

namespace AgeNexus.Infrastructure.Persistence;

public sealed class AgeNexusDbContext(DbContextOptions<AgeNexusDbContext> options) : DbContext(options)
{
    public DbSet<PlayerProfile> PlayerProfiles => Set<PlayerProfile>();
    public DbSet<AiDifficulty> AiDifficulties => Set<AiDifficulty>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchTeam> MatchTeams => Set<MatchTeam>();
    public DbSet<MatchParticipant> MatchParticipants => Set<MatchParticipant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AgeNexusDbContext).Assembly);
    }
}
