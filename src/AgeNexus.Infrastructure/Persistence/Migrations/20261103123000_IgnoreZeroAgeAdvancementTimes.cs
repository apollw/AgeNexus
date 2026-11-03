using AgeNexus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgeNexus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgeNexusDbContext))]
[Migration("20261103123000_IgnoreZeroAgeAdvancementTimes")]
public sealed class IgnoreZeroAgeAdvancementTimes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE public.player_match_statistics
            SET feudal_age_seconds = CASE WHEN feudal_age_seconds <= 0 THEN NULL ELSE feudal_age_seconds END,
                castle_age_seconds = CASE WHEN castle_age_seconds <= 0 THEN NULL ELSE castle_age_seconds END,
                imperial_age_seconds = CASE WHEN imperial_age_seconds <= 0 THEN NULL ELSE imperial_age_seconds END
            WHERE feudal_age_seconds <= 0
               OR castle_age_seconds <= 0
               OR imperial_age_seconds <= 0;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Correção de dados sem reversão: não é possível distinguir a origem de um zero antigo.
    }
}
