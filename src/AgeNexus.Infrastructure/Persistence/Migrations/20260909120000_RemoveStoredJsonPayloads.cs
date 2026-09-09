using AgeNexus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgeNexus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgeNexusDbContext))]
[Migration("20260909120000_RemoveStoredJsonPayloads")]
public sealed class RemoveStoredJsonPayloads : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE match_statistics_reports
            SET coverage_details = coverage_details - 'originalJson'
            WHERE jsonb_typeof(coverage_details) = 'object'
              AND coverage_details ? 'originalJson';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The discarded source documents cannot and should not be reconstructed.
    }
}
