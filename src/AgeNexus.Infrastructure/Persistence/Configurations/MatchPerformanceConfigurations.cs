using AgeNexus.Domain.MatchPerformance;
using AgeNexus.Domain.Matches;
using AgeNexus.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgeNexus.Infrastructure.Persistence.Configurations;

internal sealed class MatchStatisticsReportConfiguration : IEntityTypeConfiguration<MatchStatisticsReport>
{
    public void Configure(EntityTypeBuilder<MatchStatisticsReport> builder)
    {
        builder.ToTable("match_statistics_reports");
        builder.HasKey(x => x.Id).HasName("pk_match_statistics_reports");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.MatchId).HasColumnName("match_id");
        builder.Property(x => x.SubmittedByPlayerProfileId).HasColumnName("submitted_by_player_profile_id");
        builder.Property(x => x.Source).HasColumnName("source").HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(24);
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(x => x.SubmittedAtUtc).HasColumnName("submitted_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(x => x.ConfirmedAtUtc).HasColumnName("confirmed_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(x => x.AwardedAtUtc).HasColumnName("awarded_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(x => x.ReplayFileName).HasColumnName("replay_file_name").HasMaxLength(260);
        builder.Property(x => x.ReplaySha256).HasColumnName("replay_sha256").HasMaxLength(64);
        builder.Property(x => x.ExtractorVersion).HasColumnName("extractor_version").HasMaxLength(100);
        builder.Property(x => x.CoverageDetails).HasColumnName("coverage_details").HasColumnType("jsonb");
        builder.HasOne<Match>().WithOne().HasForeignKey<MatchStatisticsReport>(x => x.MatchId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_match_statistics_report_match");
        builder.HasOne<PlayerProfile>().WithMany().HasForeignKey(x => x.SubmittedByPlayerProfileId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_match_statistics_report_submitter");
        builder.HasIndex(x => x.MatchId).IsUnique().HasDatabaseName("ux_match_statistics_reports_match");
        builder.HasIndex(x => x.ReplaySha256).IsUnique().HasFilter("replay_sha256 IS NOT NULL")
            .HasDatabaseName("ux_match_statistics_reports_replay_hash");
    }
}

internal sealed class PlayerMatchStatisticsConfiguration : IEntityTypeConfiguration<PlayerMatchStatistics>
{
    public void Configure(EntityTypeBuilder<PlayerMatchStatistics> builder)
    {
        builder.ToTable("player_match_statistics", table =>
        {
            table.HasCheckConstraint("ck_player_match_statistics_explored", "explored_percent IS NULL OR (explored_percent >= 0 AND explored_percent <= 100)");
        });
        builder.HasKey(x => x.Id).HasName("pk_player_match_statistics");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.ReportId).HasColumnName("report_id");
        builder.Property(x => x.MatchId).HasColumnName("match_id");
        builder.Property(x => x.TeamId).HasColumnName("team_id");
        builder.Property(x => x.PlayerProfileId).HasColumnName("player_profile_id");
        builder.Property(x => x.Origin).HasColumnName("origin").HasConversion<string>().HasMaxLength(24);
        MapValues(builder);
        builder.HasOne<MatchStatisticsReport>().WithMany().HasForeignKey(x => x.ReportId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_player_match_statistics_report");
        builder.HasOne<Match>().WithMany().HasForeignKey(x => x.MatchId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_player_match_statistics_match");
        builder.HasOne<MatchTeam>().WithMany().HasForeignKey(x => x.TeamId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_player_match_statistics_team");
        builder.HasOne<PlayerProfile>().WithMany().HasForeignKey(x => x.PlayerProfileId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_player_match_statistics_player");
        builder.HasIndex(x => new { x.ReportId, x.PlayerProfileId }).IsUnique()
            .HasDatabaseName("ux_player_match_statistics_report_player");
    }

    private static void MapValues(EntityTypeBuilder<PlayerMatchStatistics> builder)
    {
        builder.Property(x => x.UnitsKilled).HasColumnName("units_killed");
        builder.Property(x => x.UnitsLost).HasColumnName("units_lost");
        builder.Property(x => x.BuildingsDestroyed).HasColumnName("buildings_destroyed");
        builder.Property(x => x.BuildingsLost).HasColumnName("buildings_lost");
        builder.Property(x => x.LargestArmy).HasColumnName("largest_army");
        builder.Property(x => x.PeakVillagers).HasColumnName("peak_villagers");
        builder.Property(x => x.FoodCollected).HasColumnName("food_collected");
        builder.Property(x => x.WoodCollected).HasColumnName("wood_collected");
        builder.Property(x => x.GoldCollected).HasColumnName("gold_collected");
        builder.Property(x => x.StoneCollected).HasColumnName("stone_collected");
        builder.Property(x => x.MilitaryScore).HasColumnName("military_score");
        builder.Property(x => x.EconomyScore).HasColumnName("economy_score");
        builder.Property(x => x.TechnologyScore).HasColumnName("technology_score");
        builder.Property(x => x.SocietyScore).HasColumnName("society_score");
        builder.Property(x => x.TotalScore).HasColumnName("total_score");
        builder.Property(x => x.UnitsConverted).HasColumnName("units_converted");
        builder.Property(x => x.TradeGold).HasColumnName("trade_gold");
        builder.Property(x => x.RelicGold).HasColumnName("relic_gold");
        builder.Property(x => x.TributeSent).HasColumnName("tribute_sent");
        builder.Property(x => x.TributeReceived).HasColumnName("tribute_received");
        builder.Property(x => x.ResearchCount).HasColumnName("research_count");
        builder.Property(x => x.ExploredPercent).HasColumnName("explored_percent").HasPrecision(5, 2);
        builder.Property(x => x.FeudalAgeSeconds).HasColumnName("feudal_age_seconds");
        builder.Property(x => x.CastleAgeSeconds).HasColumnName("castle_age_seconds");
        builder.Property(x => x.ImperialAgeSeconds).HasColumnName("imperial_age_seconds");
        builder.Property(x => x.EffectiveActionsPerMinute).HasColumnName("effective_actions_per_minute");
    }
}

internal sealed class StatisticsConfirmationConfiguration : IEntityTypeConfiguration<StatisticsConfirmation>
{
    public void Configure(EntityTypeBuilder<StatisticsConfirmation> builder)
    {
        builder.ToTable("statistics_confirmations");
        builder.HasKey(x => x.Id).HasName("pk_statistics_confirmations");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.ReportId).HasColumnName("report_id");
        builder.Property(x => x.TeamId).HasColumnName("team_id");
        builder.Property(x => x.ConfirmedByPlayerProfileId).HasColumnName("confirmed_by_player_profile_id");
        builder.Property(x => x.Decision).HasColumnName("decision").HasConversion<string>().HasMaxLength(24);
        builder.Property(x => x.DecidedAtUtc).HasColumnName("decided_at_utc").HasColumnType("timestamp with time zone");
        builder.HasOne<MatchStatisticsReport>().WithMany().HasForeignKey(x => x.ReportId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_statistics_confirmation_report");
        builder.HasOne<MatchTeam>().WithMany().HasForeignKey(x => x.TeamId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_statistics_confirmation_team");
        builder.HasOne<PlayerProfile>().WithMany().HasForeignKey(x => x.ConfirmedByPlayerProfileId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_statistics_confirmation_player");
        builder.HasIndex(x => new { x.ReportId, x.TeamId }).IsUnique()
            .HasDatabaseName("ux_statistics_confirmations_report_team");
    }
}

internal sealed class PlayerPerformanceScoreConfiguration : IEntityTypeConfiguration<PlayerPerformanceScore>
{
    public void Configure(EntityTypeBuilder<PlayerPerformanceScore> builder)
    {
        builder.ToTable("player_performance_scores");
        builder.HasKey(x => x.Id).HasName("pk_player_performance_scores");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.ReportId).HasColumnName("report_id");
        builder.Property(x => x.MatchId).HasColumnName("match_id");
        builder.Property(x => x.TeamId).HasColumnName("team_id");
        builder.Property(x => x.PlayerProfileId).HasColumnName("player_profile_id");
        builder.Property(x => x.Military).HasColumnName("military").HasPrecision(6, 4);
        builder.Property(x => x.Economy).HasColumnName("economy").HasPrecision(6, 4);
        builder.Property(x => x.Technology).HasColumnName("technology").HasPrecision(6, 4);
        builder.Property(x => x.Society).HasColumnName("society").HasPrecision(6, 4);
        builder.Property(x => x.Overall).HasColumnName("overall").HasPrecision(6, 4);
        builder.Property(x => x.AwardType).HasColumnName("award_type").HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.BonusPoints).HasColumnName("bonus_points");
        builder.Property(x => x.FormulaVersion).HasColumnName("formula_version").HasMaxLength(40);
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        builder.HasOne<MatchStatisticsReport>().WithMany().HasForeignKey(x => x.ReportId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_player_performance_score_report");
        builder.HasOne<Match>().WithMany().HasForeignKey(x => x.MatchId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_player_performance_score_match");
        builder.HasOne<PlayerProfile>().WithMany().HasForeignKey(x => x.PlayerProfileId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_player_performance_score_player");
        builder.HasIndex(x => new { x.ReportId, x.PlayerProfileId }).IsUnique()
            .HasDatabaseName("ux_player_performance_scores_report_player");
    }
}
