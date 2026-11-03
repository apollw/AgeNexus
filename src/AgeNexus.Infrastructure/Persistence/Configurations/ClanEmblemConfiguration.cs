using AgeNexus.Domain.Clans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgeNexus.Infrastructure.Persistence.Configurations;

internal sealed class ClanEmblemConfiguration : IEntityTypeConfiguration<ClanEmblem>
{
    public void Configure(EntityTypeBuilder<ClanEmblem> builder)
    {
        builder.ToTable("clan_emblems");
        builder.HasKey(x => x.ClanId).HasName("pk_clan_emblems");
        builder.Property(x => x.ClanId).HasColumnName("clan_id").ValueGeneratedNever();
        builder.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(30);
        builder.Property(x => x.Content).HasColumnName("content").HasColumnType("bytea");
        builder.Property(x => x.Sha256).HasColumnName("sha256").HasMaxLength(64);
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc")
            .HasColumnType("timestamp with time zone");

        builder.HasOne<Clan>().WithOne().HasForeignKey<ClanEmblem>(x => x.ClanId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_clan_emblems_clan");
    }
}
