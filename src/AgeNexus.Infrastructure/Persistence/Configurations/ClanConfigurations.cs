using AgeNexus.Domain.Clans;
using AgeNexus.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgeNexus.Infrastructure.Persistence.Configurations;

internal sealed class ClanConfiguration : IEntityTypeConfiguration<Clan>
{
    public void Configure(EntityTypeBuilder<Clan> builder)
    {
        builder.ToTable("clans");
        builder.HasKey(x => x.Id).HasName("pk_clans");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Tag).HasColumnName("tag").HasMaxLength(8).IsRequired();
        builder.Property(x => x.CreatedByPlayerProfileId).HasColumnName("created_by_player_profile_id");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        builder.HasOne<PlayerProfile>().WithMany().HasForeignKey(x => x.CreatedByPlayerProfileId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_clans_founder");
        builder.HasIndex(x => x.Name).IsUnique().HasDatabaseName("ux_clans_name");
        builder.HasIndex(x => x.Tag).IsUnique().HasDatabaseName("ux_clans_tag");
    }
}

internal sealed class ClanMembershipConfiguration : IEntityTypeConfiguration<ClanMembership>
{
    public void Configure(EntityTypeBuilder<ClanMembership> builder)
    {
        builder.ToTable("clan_memberships");
        builder.HasKey(x => x.Id).HasName("pk_clan_memberships");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.ClanId).HasColumnName("clan_id");
        builder.Property(x => x.PlayerProfileId).HasColumnName("player_profile_id");
        builder.Property(x => x.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(24);
        builder.Property(x => x.StartedAtUtc).HasColumnName("started_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(x => x.EndedAtUtc).HasColumnName("ended_at_utc").HasColumnType("timestamp with time zone");
        builder.HasOne<Clan>().WithMany().HasForeignKey(x => x.ClanId).OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_clan_memberships_clan");
        builder.HasOne<PlayerProfile>().WithMany().HasForeignKey(x => x.PlayerProfileId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_clan_memberships_player");
        builder.HasIndex(x => x.PlayerProfileId).IsUnique().HasFilter("ended_at_utc IS NULL")
            .HasDatabaseName("ux_clan_memberships_active_player");
    }
}
