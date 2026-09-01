using AgeNexus.Domain.Competition;
using AgeNexus.Domain.GameCatalog;
using AgeNexus.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgeNexus.Infrastructure.Persistence.Configurations;

internal sealed class TeamLineupConfiguration : IEntityTypeConfiguration<TeamLineup>
{
    public void Configure(EntityTypeBuilder<TeamLineup> builder)
    {
        builder.ToTable("team_lineups");
        builder.HasKey(x => x.Id).HasName("pk_team_lineups");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.GameEditionId).HasColumnName("game_edition_id");
        builder.Property(x => x.NormalizedKey).HasColumnName("normalized_key").HasMaxLength(512);
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        builder.Ignore(x => x.MemberCount);
        builder.HasOne<GameEdition>().WithMany().HasForeignKey(x => x.GameEditionId).OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_team_lineups_game_edition");
        builder.HasMany(x => x.Members).WithOne().HasForeignKey("team_lineup_id").OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_team_lineup_members_lineup");
        builder.Navigation(x => x.Members).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(x => x.NormalizedKey).IsUnique().HasDatabaseName("ux_team_lineups_key");
    }
}

internal sealed class TeamLineupMemberConfiguration : IEntityTypeConfiguration<TeamLineupMember>
{
    public void Configure(EntityTypeBuilder<TeamLineupMember> builder)
    {
        builder.ToTable("team_lineup_members");
        builder.HasKey(x => x.Id).HasName("pk_team_lineup_members");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property<Guid>("team_lineup_id").HasColumnName("team_lineup_id");
        builder.Property(x => x.PlayerProfileId).HasColumnName("player_profile_id");
        builder.Property(x => x.Position).HasColumnName("position");
        builder.HasOne<PlayerProfile>().WithMany().HasForeignKey(x => x.PlayerProfileId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_team_lineup_members_player");
        builder.HasIndex("team_lineup_id", nameof(TeamLineupMember.Position)).IsUnique()
            .HasDatabaseName("ux_team_lineup_members_position");
        builder.HasIndex("team_lineup_id", nameof(TeamLineupMember.PlayerProfileId)).IsUnique()
            .HasDatabaseName("ux_team_lineup_members_player");
    }
}
