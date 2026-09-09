using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgeNexus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgeNexusDbContext))]
[Migration("20260909150000_AddPlayerProfileClaims")]
public sealed class AddPlayerProfileClaims : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "player_profile_claims",
            schema: "public",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                application_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                player_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                requested_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                decided_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                decided_by_application_user_id = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_player_profile_claims", x => x.id);
                table.ForeignKey(
                    name: "fk_profile_claims_decider",
                    column: x => x.decided_by_application_user_id,
                    principalSchema: "identity",
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_profile_claims_profile",
                    column: x => x.player_profile_id,
                    principalSchema: "public",
                    principalTable: "player_profiles",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_profile_claims_user",
                    column: x => x.application_user_id,
                    principalSchema: "identity",
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_profile_claims_decider",
            schema: "public",
            table: "player_profile_claims",
            column: "decided_by_application_user_id");
        migrationBuilder.CreateIndex(
            name: "ix_profile_claims_status_requested",
            schema: "public",
            table: "player_profile_claims",
            columns: new[] { "status", "requested_at_utc" });
        migrationBuilder.CreateIndex(
            name: "ux_profile_claims_pending_profile",
            schema: "public",
            table: "player_profile_claims",
            column: "player_profile_id",
            unique: true,
            filter: "status = 'Pending'");
        migrationBuilder.CreateIndex(
            name: "ux_profile_claims_pending_user",
            schema: "public",
            table: "player_profile_claims",
            column: "application_user_id",
            unique: true,
            filter: "status = 'Pending'");

        migrationBuilder.Sql(
            """
            ALTER TABLE public.player_profile_claims ENABLE ROW LEVEL SECURITY;
            REVOKE ALL ON TABLE public.player_profile_claims FROM anon, authenticated;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "player_profile_claims", schema: "public");
    }
}
