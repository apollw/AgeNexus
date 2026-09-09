using AgeNexus.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgeNexus.Infrastructure.Persistence.Configurations;

internal sealed class PlayerProfileAvatarConfiguration : IEntityTypeConfiguration<PlayerProfileAvatar>
{
    public void Configure(EntityTypeBuilder<PlayerProfileAvatar> builder)
    {
        builder.ToTable("player_profile_avatars");
        builder.HasKey(x => x.PlayerProfileId).HasName("pk_player_profile_avatars");
        builder.Property(x => x.PlayerProfileId).HasColumnName("player_profile_id").ValueGeneratedNever();
        builder.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(30);
        builder.Property(x => x.Content).HasColumnName("content").HasColumnType("bytea");
        builder.Property(x => x.Sha256).HasColumnName("sha256").HasMaxLength(64);
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc")
            .HasColumnType("timestamp with time zone");

        builder.HasOne<PlayerProfile>().WithOne().HasForeignKey<PlayerProfileAvatar>(x => x.PlayerProfileId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_player_profile_avatars_profile");
    }
}
