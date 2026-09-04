using AgeNexus.Domain.Competition;
using AgeNexus.Domain.GameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgeNexus.Infrastructure.Persistence.Configurations;

internal sealed class SeasonConfiguration : IEntityTypeConfiguration<Season>
{
    public void Configure(EntityTypeBuilder<Season> builder)
    {
        builder.ToTable("seasons", table => table.HasCheckConstraint("ck_seasons_interval", "ends_at_utc > starts_at_utc"));
        builder.HasKey(x => x.Id).HasName("pk_seasons");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.GameEditionId).HasColumnName("game_edition_id");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        builder.Property(x => x.StartsAtUtc).HasColumnName("starts_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(x => x.EndsAtUtc).HasColumnName("ends_at_utc").HasColumnType("timestamp with time zone");
        builder.HasOne<GameEdition>().WithMany().HasForeignKey(x => x.GameEditionId).OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_seasons_game_edition");
        builder.HasIndex(x => new { x.GameEditionId, x.StartsAtUtc }).IsUnique().HasDatabaseName("ux_seasons_edition_start");
    }
}

internal sealed class RatingEventConfiguration : IEntityTypeConfiguration<RatingEvent>
{
    public void Configure(EntityTypeBuilder<RatingEvent> builder)
    {
        builder.ToTable("rating_events");
        ConfigureCommon(builder);
        builder.Property(x => x.Scope).HasColumnName("scope").HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.Delta).HasColumnName("delta").HasPrecision(12, 2);
        builder.Property(x => x.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.ReversesEventId).HasColumnName("reverses_event_id");
        builder.HasIndex(x => new { x.MatchId, x.BeneficiaryId, x.SeasonId, x.Scope, x.RuleVersion, x.Kind })
            .IsUnique().HasDatabaseName("ux_rating_events_idempotency");
        builder.HasIndex(x => x.ReversesEventId).IsUnique().HasFilter("reverses_event_id IS NOT NULL")
            .HasDatabaseName("ux_rating_events_reversal");
        builder.HasIndex(x => new { x.Scope, x.SeasonId, x.BeneficiaryId })
            .HasDatabaseName("ix_rating_events_ranking");
    }

    private static void ConfigureCommon(EntityTypeBuilder<RatingEvent> builder)
    {
        builder.HasKey(x => x.Id).HasName("pk_rating_events");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.MatchId).HasColumnName("match_id");
        builder.Property(x => x.BeneficiaryId).HasColumnName("beneficiary_id");
        builder.Property(x => x.SeasonId).HasColumnName("season_id");
        builder.Property(x => x.RuleVersion).HasColumnName("rule_version").HasMaxLength(32);
        builder.Property(x => x.CalculationDetails).HasColumnName("calculation_details").HasColumnType("jsonb");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
    }
}

internal sealed class PointEventConfiguration : IEntityTypeConfiguration<PointEvent>
{
    public void Configure(EntityTypeBuilder<PointEvent> builder)
    {
        builder.ToTable("point_events");
        builder.HasKey(x => x.Id).HasName("pk_point_events");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.MatchId).HasColumnName("match_id");
        builder.Property(x => x.BeneficiaryId).HasColumnName("beneficiary_id");
        builder.Property(x => x.SeasonId).HasColumnName("season_id");
        builder.Property(x => x.Scope).HasColumnName("scope").HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.Points).HasColumnName("points").HasPrecision(12, 2);
        builder.Property(x => x.RuleVersion).HasColumnName("rule_version").HasMaxLength(32);
        builder.Property(x => x.CalculationDetails).HasColumnName("calculation_details").HasColumnType("jsonb");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(x => x.SourceKey).HasColumnName("source_key").HasMaxLength(256);
        builder.Property(x => x.EvidenceLevel).HasColumnName("evidence_level").HasConversion<string>().HasMaxLength(24);
        builder.Property(x => x.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.ReversesEventId).HasColumnName("reverses_event_id");
        builder.HasIndex(x => new { x.MatchId, x.BeneficiaryId, x.SeasonId, x.Scope, x.RuleVersion, x.Kind })
            .IsUnique().HasDatabaseName("ux_point_events_idempotency");
        builder.HasIndex(x => x.ReversesEventId).IsUnique().HasFilter("reverses_event_id IS NOT NULL")
            .HasDatabaseName("ux_point_events_reversal");
        builder.HasIndex(x => new { x.BeneficiaryId, x.SeasonId, x.SourceKey })
            .HasDatabaseName("ix_point_events_repetition");
        builder.HasIndex(x => new { x.Scope, x.SeasonId, x.BeneficiaryId })
            .HasDatabaseName("ix_point_events_ranking");
        builder.HasIndex(x => new { x.Scope, x.EvidenceLevel, x.SeasonId, x.BeneficiaryId })
            .HasDatabaseName("ix_point_events_verified_ranking");
    }
}
