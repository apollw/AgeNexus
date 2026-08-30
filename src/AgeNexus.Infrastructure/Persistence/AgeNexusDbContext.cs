using AgeNexus.Domain.GameCatalog;
using AgeNexus.Domain.Matches;
using AgeNexus.Domain.Players;
using AgeNexus.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AgeNexus.Infrastructure.Persistence;

public sealed class AgeNexusDbContext(DbContextOptions<AgeNexusDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<PlayerProfile> PlayerProfiles => Set<PlayerProfile>();
    public DbSet<AiDifficulty> AiDifficulties => Set<AiDifficulty>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchTeam> MatchTeams => Set<MatchTeam>();
    public DbSet<MatchParticipant> MatchParticipants => Set<MatchParticipant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("public");
        ConfigureIdentity(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AgeNexusDbContext).Assembly);
    }

    private static void ConfigureIdentity(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");

        modelBuilder.Entity<ApplicationUser>(builder =>
        {
            builder.ToTable("users", "identity");
            builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(x => x.UserName).HasColumnName("user_name");
            builder.Property(x => x.NormalizedUserName).HasColumnName("normalized_user_name");
            builder.Property(x => x.Email).HasColumnName("email");
            builder.Property(x => x.NormalizedEmail).HasColumnName("normalized_email");
            builder.Property(x => x.EmailConfirmed).HasColumnName("email_confirmed");
            builder.Property(x => x.PasswordHash).HasColumnName("password_hash");
            builder.Property(x => x.SecurityStamp).HasColumnName("security_stamp");
            builder.Property(x => x.ConcurrencyStamp).HasColumnName("concurrency_stamp");
            builder.Property(x => x.PhoneNumber).HasColumnName("phone_number");
            builder.Property(x => x.PhoneNumberConfirmed).HasColumnName("phone_number_confirmed");
            builder.Property(x => x.TwoFactorEnabled).HasColumnName("two_factor_enabled");
            builder.Property(x => x.LockoutEnd).HasColumnName("lockout_end");
            builder.Property(x => x.LockoutEnabled).HasColumnName("lockout_enabled");
            builder.Property(x => x.AccessFailedCount).HasColumnName("access_failed_count");
            builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        });

        modelBuilder.Entity<IdentityRole<Guid>>(builder =>
        {
            builder.ToTable("roles", "identity");
            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.Name).HasColumnName("name");
            builder.Property(x => x.NormalizedName).HasColumnName("normalized_name");
            builder.Property(x => x.ConcurrencyStamp).HasColumnName("concurrency_stamp");
        });

        modelBuilder.Entity<IdentityUserClaim<Guid>>(builder =>
        {
            builder.ToTable("user_claims", "identity");
            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.UserId).HasColumnName("user_id");
            builder.Property(x => x.ClaimType).HasColumnName("claim_type");
            builder.Property(x => x.ClaimValue).HasColumnName("claim_value");
        });

        modelBuilder.Entity<IdentityUserLogin<Guid>>(builder =>
        {
            builder.ToTable("user_logins", "identity");
            builder.Property(x => x.LoginProvider).HasColumnName("login_provider");
            builder.Property(x => x.ProviderKey).HasColumnName("provider_key");
            builder.Property(x => x.ProviderDisplayName).HasColumnName("provider_display_name");
            builder.Property(x => x.UserId).HasColumnName("user_id");
        });

        modelBuilder.Entity<IdentityUserToken<Guid>>(builder =>
        {
            builder.ToTable("user_tokens", "identity");
            builder.Property(x => x.UserId).HasColumnName("user_id");
            builder.Property(x => x.LoginProvider).HasColumnName("login_provider");
            builder.Property(x => x.Name).HasColumnName("name");
            builder.Property(x => x.Value).HasColumnName("value");
        });

        modelBuilder.Entity<IdentityUserRole<Guid>>(builder =>
        {
            builder.ToTable("user_roles", "identity");
            builder.Property(x => x.UserId).HasColumnName("user_id");
            builder.Property(x => x.RoleId).HasColumnName("role_id");
        });

        modelBuilder.Entity<IdentityRoleClaim<Guid>>(builder =>
        {
            builder.ToTable("role_claims", "identity");
            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.RoleId).HasColumnName("role_id");
            builder.Property(x => x.ClaimType).HasColumnName("claim_type");
            builder.Property(x => x.ClaimValue).HasColumnName("claim_value");
        });
    }
}
