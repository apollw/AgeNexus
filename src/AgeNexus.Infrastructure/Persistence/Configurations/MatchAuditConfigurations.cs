using AgeNexus.Domain.Matches;
using AgeNexus.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgeNexus.Infrastructure.Persistence.Configurations;

internal sealed class MatchConfirmationConfiguration : IEntityTypeConfiguration<MatchConfirmation>
{
    public void Configure(EntityTypeBuilder<MatchConfirmation> builder)
    {
        builder.ToTable("match_confirmations");
        builder.HasKey(x => x.Id).HasName("pk_match_confirmations");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.MatchId).HasColumnName("match_id");
        builder.Property(x => x.PlayerProfileId).HasColumnName("player_profile_id");
        builder.Property(x => x.Decision).HasColumnName("decision").HasConversion<string>().HasMaxLength(24);
        builder.Property(x => x.DecidedAtUtc).HasColumnName("decided_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(x => x.Comment).HasColumnName("comment").HasMaxLength(1000);
        builder.HasOne<Match>().WithMany().HasForeignKey(x => x.MatchId).OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_match_confirmations_match");
        builder.HasOne<PlayerProfile>().WithMany().HasForeignKey(x => x.PlayerProfileId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_match_confirmations_player");
        builder.HasIndex(x => new { x.MatchId, x.PlayerProfileId }).IsUnique()
            .HasDatabaseName("ux_match_confirmations_match_player");
    }
}

internal sealed class MatchRevisionConfiguration : IEntityTypeConfiguration<MatchRevision>
{
    public void Configure(EntityTypeBuilder<MatchRevision> builder)
    {
        builder.ToTable("match_revisions");
        builder.HasKey(x => x.Id).HasName("pk_match_revisions");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.MatchId).HasColumnName("match_id");
        builder.Property(x => x.ChangedByApplicationUserId).HasColumnName("changed_by_application_user_id");
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(1000);
        builder.Property(x => x.Snapshot).HasColumnName("snapshot").HasColumnType("jsonb");
        builder.Property(x => x.ChangedAtUtc).HasColumnName("changed_at_utc").HasColumnType("timestamp with time zone");
        builder.HasOne<Match>().WithMany().HasForeignKey(x => x.MatchId).OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_match_revisions_match");
        builder.HasIndex(x => new { x.MatchId, x.ChangedAtUtc }).HasDatabaseName("ix_match_revisions_match_time");
    }
}
