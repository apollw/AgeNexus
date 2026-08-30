using AgeNexus.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgeNexus.Infrastructure.Persistence.Configurations;

internal sealed class PlayerProfileConfiguration : IEntityTypeConfiguration<PlayerProfile>
{
    public void Configure(EntityTypeBuilder<PlayerProfile> builder)
    {
        builder.ToTable("player_profiles");
        builder.HasKey(x => x.Id).HasName("pk_player_profiles");

        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.ApplicationUserId).HasColumnName("application_user_id");
        builder.Ignore(x => x.HasUserAccount);

        builder.HasIndex(x => x.DisplayName).HasDatabaseName("ix_player_profiles_display_name");
        builder.HasIndex(x => x.ApplicationUserId)
            .IsUnique()
            .HasFilter("application_user_id IS NOT NULL")
            .HasDatabaseName("ux_player_profiles_application_user_id");
    }
}
