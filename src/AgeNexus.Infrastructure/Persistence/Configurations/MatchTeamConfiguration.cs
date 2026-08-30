using AgeNexus.Domain.Matches;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgeNexus.Infrastructure.Persistence.Configurations;

internal sealed class MatchTeamConfiguration : IEntityTypeConfiguration<MatchTeam>
{
    public void Configure(EntityTypeBuilder<MatchTeam> builder)
    {
        builder.ToTable("match_teams");
        builder.HasKey(x => x.Id).HasName("pk_match_teams");

        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.Position).HasColumnName("position");
        builder.Property(x => x.Result).HasColumnName("result").HasConversion<string>().HasMaxLength(24);
        builder.Property<Guid>("match_id").HasColumnName("match_id");
        builder.Ignore(x => x.HumanCount);
        builder.Ignore(x => x.AiCount);

        builder.HasMany(x => x.Participants)
            .WithOne()
            .HasForeignKey("team_id")
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_match_participants_team");

        builder.Navigation(x => x.Participants).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex("match_id", nameof(MatchTeam.Position))
            .IsUnique()
            .HasDatabaseName("ux_match_teams_match_position");
    }
}
