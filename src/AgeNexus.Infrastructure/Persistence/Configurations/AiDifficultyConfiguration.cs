using AgeNexus.Domain.GameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgeNexus.Infrastructure.Persistence.Configurations;

internal sealed class AiDifficultyConfiguration : IEntityTypeConfiguration<AiDifficulty>
{
    public void Configure(EntityTypeBuilder<AiDifficulty> builder)
    {
        builder.ToTable("ai_difficulties", table =>
            table.HasCheckConstraint("ck_ai_difficulties_internal_level", "internal_level BETWEEN 1 AND 5"));
        builder.HasKey(x => x.Id).HasName("pk_ai_difficulties");

        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.GameEditionId).HasColumnName("game_edition_id");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(80).IsRequired();
        builder.Property(x => x.InternalLevel).HasColumnName("internal_level");

        builder.HasIndex(x => new { x.GameEditionId, x.Name })
            .IsUnique()
            .HasDatabaseName("ux_ai_difficulties_edition_name");
    }
}
