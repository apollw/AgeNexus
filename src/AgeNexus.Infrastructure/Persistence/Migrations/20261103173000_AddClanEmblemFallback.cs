using AgeNexus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgeNexus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgeNexusDbContext))]
[Migration("20261103173000_AddClanEmblemFallback")]
public sealed class AddClanEmblemFallback : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "clan_emblems",
            schema: "public",
            columns: table => new
            {
                clan_id = table.Column<Guid>(type: "uuid", nullable: false),
                content_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                content = table.Column<byte[]>(type: "bytea", nullable: false),
                sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_clan_emblems", x => x.clan_id);
                table.ForeignKey(
                    name: "fk_clan_emblems_clan",
                    column: x => x.clan_id,
                    principalSchema: "public",
                    principalTable: "clans",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.Sql("""
            ALTER TABLE public.clan_emblems ENABLE ROW LEVEL SECURITY;
            REVOKE ALL ON TABLE public.clan_emblems FROM anon, authenticated;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "clan_emblems", schema: "public");
}
