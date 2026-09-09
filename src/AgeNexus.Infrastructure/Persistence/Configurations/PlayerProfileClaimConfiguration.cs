using AgeNexus.Domain.Players;
using AgeNexus.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgeNexus.Infrastructure.Persistence.Configurations;

internal sealed class PlayerProfileClaimConfiguration : IEntityTypeConfiguration<PlayerProfileClaim>
{
    public void Configure(EntityTypeBuilder<PlayerProfileClaim> builder)
    {
        builder.ToTable("player_profile_claims");
        builder.HasKey(x => x.Id).HasName("pk_player_profile_claims");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.ApplicationUserId).HasColumnName("application_user_id");
        builder.Property(x => x.PlayerProfileId).HasColumnName("player_profile_id");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.RequestedAtUtc).HasColumnName("requested_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(x => x.DecidedAtUtc).HasColumnName("decided_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(x => x.DecidedByApplicationUserId).HasColumnName("decided_by_application_user_id");

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ApplicationUserId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_profile_claims_user");
        builder.HasOne<PlayerProfile>().WithMany().HasForeignKey(x => x.PlayerProfileId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_profile_claims_profile");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.DecidedByApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_profile_claims_decider");

        builder.HasIndex(x => x.ApplicationUserId).IsUnique()
            .HasFilter("status = 'Pending'").HasDatabaseName("ux_profile_claims_pending_user");
        builder.HasIndex(x => x.PlayerProfileId).IsUnique()
            .HasFilter("status = 'Pending'").HasDatabaseName("ux_profile_claims_pending_profile");
        builder.HasIndex(x => x.DecidedByApplicationUserId)
            .HasDatabaseName("ix_profile_claims_decider");
        builder.HasIndex(x => new { x.Status, x.RequestedAtUtc })
            .HasDatabaseName("ix_profile_claims_status_requested");
    }
}
