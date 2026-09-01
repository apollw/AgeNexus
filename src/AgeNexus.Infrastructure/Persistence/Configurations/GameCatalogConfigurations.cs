using AgeNexus.Domain.GameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgeNexus.Infrastructure.Persistence.Configurations;

internal sealed class GameConfiguration : IEntityTypeConfiguration<Game>
{
    public void Configure(EntityTypeBuilder<Game> builder)
    {
        builder.ToTable("games");
        builder.HasKey(x => x.Id).HasName("pk_games");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        builder.Property(x => x.Slug).HasColumnName("slug").HasMaxLength(80).IsRequired();
        builder.HasIndex(x => x.Slug).IsUnique().HasDatabaseName("ux_games_slug");
    }
}

internal sealed class GameEditionConfiguration : IEntityTypeConfiguration<GameEdition>
{
    public void Configure(EntityTypeBuilder<GameEdition> builder)
    {
        builder.ToTable("game_editions");
        builder.HasKey(x => x.Id).HasName("pk_game_editions");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.GameId).HasColumnName("game_id");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        builder.Property(x => x.Slug).HasColumnName("slug").HasMaxLength(80).IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        builder.HasOne<Game>().WithMany().HasForeignKey(x => x.GameId).OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_game_editions_game");
        builder.HasIndex(x => new { x.GameId, x.Slug }).IsUnique().HasDatabaseName("ux_game_editions_game_slug");
    }
}

internal sealed class FactionConfiguration : IEntityTypeConfiguration<Faction>
{
    public void Configure(EntityTypeBuilder<Faction> builder)
    {
        builder.ToTable("factions");
        builder.HasKey(x => x.Id).HasName("pk_factions");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.GameEditionId).HasColumnName("game_edition_id");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        builder.Property(x => x.Slug).HasColumnName("slug").HasMaxLength(80).IsRequired();
        builder.Property(x => x.ImageUrl).HasColumnName("image_url").HasMaxLength(500);
        builder.HasOne<GameEdition>().WithMany().HasForeignKey(x => x.GameEditionId).OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_factions_game_edition");
        builder.HasIndex(x => new { x.GameEditionId, x.Slug }).IsUnique().HasDatabaseName("ux_factions_edition_slug");
    }
}

internal sealed class MapDefinitionConfiguration : IEntityTypeConfiguration<MapDefinition>
{
    public void Configure(EntityTypeBuilder<MapDefinition> builder)
    {
        builder.ToTable("map_definitions");
        builder.HasKey(x => x.Id).HasName("pk_map_definitions");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.GameEditionId).HasColumnName("game_edition_id");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        builder.Property(x => x.Slug).HasColumnName("slug").HasMaxLength(80).IsRequired();
        builder.HasOne<GameEdition>().WithMany().HasForeignKey(x => x.GameEditionId).OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_maps_game_edition");
        builder.HasIndex(x => new { x.GameEditionId, x.Slug }).IsUnique().HasDatabaseName("ux_maps_edition_slug");
    }
}

internal sealed class GamePatchConfiguration : IEntityTypeConfiguration<GamePatch>
{
    public void Configure(EntityTypeBuilder<GamePatch> builder)
    {
        builder.ToTable("game_patches");
        builder.HasKey(x => x.Id).HasName("pk_game_patches");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.GameEditionId).HasColumnName("game_edition_id");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        builder.Property(x => x.EffectiveFromUtc).HasColumnName("effective_from_utc").HasColumnType("timestamp with time zone");
        builder.Property(x => x.EffectiveToUtc).HasColumnName("effective_to_utc").HasColumnType("timestamp with time zone");
        builder.HasOne<GameEdition>().WithMany().HasForeignKey(x => x.GameEditionId).OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_game_patches_game_edition");
        builder.HasIndex(x => new { x.GameEditionId, x.EffectiveFromUtc }).IsUnique()
            .HasDatabaseName("ux_game_patches_edition_start");
    }
}
