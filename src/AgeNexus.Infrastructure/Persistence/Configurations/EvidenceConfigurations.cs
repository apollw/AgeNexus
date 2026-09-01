using AgeNexus.Domain.EvidenceAndModeration;
using AgeNexus.Domain.GameCatalog;
using AgeNexus.Domain.Matches;
using AgeNexus.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgeNexus.Infrastructure.Persistence.Configurations;

internal sealed class ChallengeTicketConfiguration : IEntityTypeConfiguration<ChallengeTicket>
{
    public void Configure(EntityTypeBuilder<ChallengeTicket> builder)
    {
        builder.ToTable("challenge_tickets");
        builder.HasKey(x => x.Id).HasName("pk_challenge_tickets");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.PlayerProfileId).HasColumnName("player_profile_id");
        builder.Property(x => x.GameEditionId).HasColumnName("game_edition_id");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(16);
        builder.Property(x => x.ConfigurationFingerprint).HasColumnName("configuration_fingerprint").HasMaxLength(128);
        builder.Property(x => x.IssuedAtUtc).HasColumnName("issued_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(x => x.ExpiresAtUtc).HasColumnName("expires_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(x => x.UsedAtUtc).HasColumnName("used_at_utc").HasColumnType("timestamp with time zone");
        builder.Ignore(x => x.IsUsed);
        builder.HasOne<PlayerProfile>().WithMany().HasForeignKey(x => x.PlayerProfileId).OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_challenge_tickets_player");
        builder.HasOne<GameEdition>().WithMany().HasForeignKey(x => x.GameEditionId).OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_challenge_tickets_edition");
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ux_challenge_tickets_code");
    }
}

internal sealed class MatchEvidenceConfiguration : IEntityTypeConfiguration<MatchEvidence>
{
    public void Configure(EntityTypeBuilder<MatchEvidence> builder)
    {
        builder.ToTable("match_evidence");
        builder.HasKey(x => x.Id).HasName("pk_match_evidence");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.MatchId).HasColumnName("match_id");
        builder.Property(x => x.SubmittedByPlayerProfileId).HasColumnName("submitted_by_player_profile_id");
        builder.Property(x => x.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.SubmittedAtUtc).HasColumnName("submitted_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(x => x.ObjectKey).HasColumnName("object_key").HasMaxLength(500);
        builder.Property(x => x.ExternalUrl).HasColumnName("external_url").HasMaxLength(1000);
        builder.Property(x => x.Sha256).HasColumnName("sha256").HasMaxLength(64);
        builder.HasOne<Match>().WithMany().HasForeignKey(x => x.MatchId).OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_match_evidence_match");
        builder.HasOne<PlayerProfile>().WithMany().HasForeignKey(x => x.SubmittedByPlayerProfileId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_match_evidence_submitter");
        builder.HasIndex(x => x.Sha256).IsUnique().HasFilter("sha256 IS NOT NULL")
            .HasDatabaseName("ux_match_evidence_sha256");
    }
}

internal sealed class VerificationDecisionConfiguration : IEntityTypeConfiguration<VerificationDecision>
{
    public void Configure(EntityTypeBuilder<VerificationDecision> builder)
    {
        builder.ToTable("verification_decisions");
        builder.HasKey(x => x.Id).HasName("pk_verification_decisions");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.MatchId).HasColumnName("match_id");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(1000);
        builder.Property(x => x.DecidedAtUtc).HasColumnName("decided_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(x => x.DecidedByApplicationUserId).HasColumnName("decided_by_application_user_id");
        builder.Ignore(x => x.EvidenceLevel);
        builder.HasOne<Match>().WithMany().HasForeignKey(x => x.MatchId).OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_verification_decisions_match");
        builder.HasIndex(x => new { x.MatchId, x.DecidedAtUtc }).HasDatabaseName("ix_verification_decisions_match_time");
    }
}
