using AgeNexus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgeNexus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgeNexusDbContext))]
[Migration("20261030120000_AddCivilizationDrafts")]
public sealed class AddCivilizationDrafts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE public.civilization_drafts (
                id uuid NOT NULL,
                share_code character varying(12) NOT NULL,
                created_by_player_profile_id uuid NOT NULL,
                game_edition_id uuid NOT NULL,
                map_definition_id uuid NULL,
                game_mode character varying(32) NOT NULL,
                status character varying(24) NOT NULL,
                version integer NOT NULL DEFAULT 1,
                state_json jsonb NOT NULL,
                created_at_utc timestamp with time zone NOT NULL,
                updated_at_utc timestamp with time zone NOT NULL,
                CONSTRAINT pk_civilization_drafts PRIMARY KEY (id),
                CONSTRAINT fk_civilization_drafts_creator FOREIGN KEY (created_by_player_profile_id)
                    REFERENCES public.player_profiles (id) ON DELETE RESTRICT,
                CONSTRAINT fk_civilization_drafts_game_edition FOREIGN KEY (game_edition_id)
                    REFERENCES public.game_editions (id) ON DELETE RESTRICT,
                CONSTRAINT fk_civilization_drafts_map FOREIGN KEY (map_definition_id)
                    REFERENCES public.map_definitions (id) ON DELETE SET NULL
            );
            CREATE UNIQUE INDEX ux_civilization_drafts_share_code ON public.civilization_drafts (share_code);
            CREATE INDEX ix_civilization_drafts_edition_status ON public.civilization_drafts (game_edition_id, status);
            CREATE INDEX ix_civilization_drafts_updated_at ON public.civilization_drafts (updated_at_utc DESC);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS public.civilization_drafts;");
    }
}
