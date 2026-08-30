using AgeNexus.Domain.Matches;
using AgeNexus.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgeNexus.Infrastructure.Persistence.Configurations;

internal sealed class MatchConfiguration : IEntityTypeConfiguration<Match>
{
    public void Configure(EntityTypeBuilder<Match> builder)
    {
        builder.ToTable("matches");
        builder.HasKey(x => x.Id).HasName("pk_matches");

        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.GameEditionId).HasColumnName("game_edition_id");
        builder.Property(x => x.CreatedByPlayerProfileId).HasColumnName("created_by_player_profile_id");
        builder.Property(x => x.PlayedAtUtc).HasColumnName("played_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(x => x.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Nature).HasColumnName("nature").HasConversion<string>().HasMaxLength(24);
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32);
        builder.Ignore(x => x.CompetitiveFormatLabel);

        builder.HasOne<PlayerProfile>()
            .WithMany()
            .HasForeignKey(x => x.CreatedByPlayerProfileId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_matches_created_by_player_profile");

        builder.HasMany(x => x.Teams)
            .WithOne()
            .HasForeignKey("match_id")
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_match_teams_match");

        builder.Navigation(x => x.Teams).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(x => x.CreatedByPlayerProfileId)
            .HasDatabaseName("ix_matches_created_by_player_profile");
        builder.HasIndex(x => x.PlayedAtUtc).HasDatabaseName("ix_matches_played_at_utc");
        builder.HasIndex(x => new { x.GameEditionId, x.Status }).HasDatabaseName("ix_matches_edition_status");
    }
}
