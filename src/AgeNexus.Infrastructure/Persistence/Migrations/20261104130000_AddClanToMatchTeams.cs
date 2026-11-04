using AgeNexus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgeNexus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgeNexusDbContext))]
[Migration("20261104130000_AddClanToMatchTeams")]
public sealed class AddClanToMatchTeams : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "clan_id", schema: "public", table: "match_teams",
            type: "uuid", nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_match_teams_clan_id", schema: "public", table: "match_teams", column: "clan_id");

        migrationBuilder.AddForeignKey(
            name: "fk_match_teams_clan", schema: "public", table: "match_teams", column: "clan_id",
            principalSchema: "public", principalTable: "clans", principalColumn: "id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "fk_match_teams_clan", schema: "public", table: "match_teams");
        migrationBuilder.DropIndex(name: "ix_match_teams_clan_id", schema: "public", table: "match_teams");
        migrationBuilder.DropColumn(name: "clan_id", schema: "public", table: "match_teams");
    }
}
