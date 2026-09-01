using AgeNexus.Domain.GameCatalog;
using AgeNexus.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgeNexus.Infrastructure.Persistence.Configurations;

internal sealed class PlayerFavoriteFactionConfiguration : IEntityTypeConfiguration<PlayerFavoriteFaction>
{
    public void Configure(EntityTypeBuilder<PlayerFavoriteFaction> builder)
    {
        builder.ToTable("player_favorite_factions", table =>
            table.HasCheckConstraint("ck_player_favorite_factions_priority", "priority BETWEEN 1 AND 5"));
        builder.HasKey(x => x.Id).HasName("pk_player_favorite_factions");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.PlayerProfileId).HasColumnName("player_profile_id");
        builder.Property(x => x.FactionId).HasColumnName("faction_id");
        builder.Property(x => x.Priority).HasColumnName("priority");
        builder.HasOne<PlayerProfile>().WithMany().HasForeignKey(x => x.PlayerProfileId).OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_player_favorite_factions_player");
        builder.HasOne<Faction>().WithMany().HasForeignKey(x => x.FactionId).OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_player_favorite_factions_faction");
        builder.HasIndex(x => new { x.PlayerProfileId, x.FactionId }).IsUnique()
            .HasDatabaseName("ux_player_favorite_factions_player_faction");
        builder.HasIndex(x => new { x.PlayerProfileId, x.Priority }).IsUnique()
            .HasDatabaseName("ux_player_favorite_factions_player_priority");
    }
}
