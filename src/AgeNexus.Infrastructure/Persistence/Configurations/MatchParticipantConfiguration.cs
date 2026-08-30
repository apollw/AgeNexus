using AgeNexus.Domain.GameCatalog;
using AgeNexus.Domain.Matches;
using AgeNexus.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgeNexus.Infrastructure.Persistence.Configurations;

internal sealed class MatchParticipantConfiguration : IEntityTypeConfiguration<MatchParticipant>
{
    public void Configure(EntityTypeBuilder<MatchParticipant> builder)
    {
        builder.ToTable("match_participants", table => table.HasCheckConstraint(
            "ck_match_participants_identity",
            "(type = 'Human' AND player_profile_id IS NOT NULL AND ai_difficulty_id IS NULL) OR " +
            "(type = 'ArtificialIntelligence' AND player_profile_id IS NULL AND ai_difficulty_id IS NOT NULL)"));
        builder.HasKey(x => x.Id).HasName("pk_match_participants");

        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property<Guid>("team_id").HasColumnName("team_id");
        builder.Property(x => x.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.PlayerProfileId).HasColumnName("player_profile_id");
        builder.Property(x => x.AiDifficultyId).HasColumnName("ai_difficulty_id");
        builder.Property(x => x.FactionId).HasColumnName("faction_id");
        builder.Property(x => x.FactionSelection).HasColumnName("faction_selection").HasConversion<string>().HasMaxLength(16);

        builder.HasOne<PlayerProfile>()
            .WithMany()
            .HasForeignKey(x => x.PlayerProfileId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_match_participants_player_profile");

        builder.HasOne<AiDifficulty>()
            .WithMany()
            .HasForeignKey(x => x.AiDifficultyId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_match_participants_ai_difficulty");

        builder.HasIndex(x => x.PlayerProfileId).HasDatabaseName("ix_match_participants_player_profile");
        builder.HasIndex(x => x.AiDifficultyId).HasDatabaseName("ix_match_participants_ai_difficulty");
        builder.HasIndex(x => x.FactionId).HasDatabaseName("ix_match_participants_faction");
        builder.HasIndex("team_id").HasDatabaseName("ix_match_participants_team");
    }
}
