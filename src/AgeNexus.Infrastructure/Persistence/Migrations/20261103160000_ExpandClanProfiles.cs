using AgeNexus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgeNexus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgeNexusDbContext))]
[Migration("20261103160000_ExpandClanProfiles")]
public sealed class ExpandClanProfiles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "description", schema: "public", table: "clans", type: "character varying(500)", maxLength: 500, nullable: true);
        migrationBuilder.AddColumn<string>(name: "motto", schema: "public", table: "clans", type: "character varying(120)", maxLength: 120, nullable: true);
        migrationBuilder.AddColumn<string>(name: "image_url", schema: "public", table: "clans", type: "character varying(500)", maxLength: 500, nullable: true);
        migrationBuilder.AddColumn<bool>(name: "is_recruiting", schema: "public", table: "clans", type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<string>(name: "title", schema: "public", table: "clan_memberships", type: "character varying(80)", maxLength: 80, nullable: true);
        migrationBuilder.AddColumn<string>(name: "note", schema: "public", table: "clan_memberships", type: "character varying(300)", maxLength: 300, nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "description", schema: "public", table: "clans");
        migrationBuilder.DropColumn(name: "motto", schema: "public", table: "clans");
        migrationBuilder.DropColumn(name: "image_url", schema: "public", table: "clans");
        migrationBuilder.DropColumn(name: "is_recruiting", schema: "public", table: "clans");
        migrationBuilder.DropColumn(name: "title", schema: "public", table: "clan_memberships");
        migrationBuilder.DropColumn(name: "note", schema: "public", table: "clan_memberships");
    }
}
