using AgeNexus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgeNexus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgeNexusDbContext))]
[Migration("20260909170000_AddDatabaseProfileAvatars")]
public sealed class AddDatabaseProfileAvatars : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "player_profile_avatars",
            schema: "public",
            columns: table => new
            {
                player_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                content_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                content = table.Column<byte[]>(type: "bytea", nullable: false),
                sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_player_profile_avatars", x => x.player_profile_id);
                table.ForeignKey(
                    name: "fk_player_profile_avatars_profile",
                    column: x => x.player_profile_id,
                    principalSchema: "public",
                    principalTable: "player_profiles",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.Sql(
            """
            ALTER TABLE public.player_profile_avatars ENABLE ROW LEVEL SECURITY;
            REVOKE ALL ON TABLE public.player_profile_avatars FROM anon, authenticated;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "player_profile_avatars", schema: "public");
    }
}
