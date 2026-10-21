using AgeNexus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgeNexus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgeNexusDbContext))]
[Migration("20261021140000_AddMatchGameMode")]
public sealed class AddMatchGameMode : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "game_mode",
            schema: "public",
            table: "matches",
            type: "character varying(32)",
            maxLength: 32,
            nullable: false,
            defaultValue: "Standard");

        migrationBuilder.CreateIndex(
            name: "ix_matches_game_mode",
            schema: "public",
            table: "matches",
            column: "game_mode");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_matches_game_mode",
            schema: "public",
            table: "matches");

        migrationBuilder.DropColumn(
            name: "game_mode",
            schema: "public",
            table: "matches");
    }
}
