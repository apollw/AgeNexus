using AgeNexus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgeNexus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgeNexusDbContext))]
[Migration("20260917120000_AddAiDifficultyCatalog")]
public sealed class AddAiDifficultyCatalog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_ai_difficulties_internal_level",
            schema: "public",
            table: "ai_difficulties");

        migrationBuilder.AddCheckConstraint(
            name: "ck_ai_difficulties_internal_level",
            schema: "public",
            table: "ai_difficulties",
            sql: "internal_level BETWEEN 1 AND 6");

        migrationBuilder.Sql("""
            WITH target_editions AS (
                SELECT edition.id
                FROM public.game_editions AS edition
                INNER JOIN public.games AS game ON game.id = edition.game_id
                WHERE edition.is_active
                  AND (
                      (
                          lower(game.name) LIKE '%age of empires%'
                          AND (game.name LIKE '%II%' OR game.name LIKE '%2%')
                          AND (lower(edition.name) LIKE '%definitive%' OR upper(trim(edition.name)) = 'DE')
                      )
                      OR (SELECT count(*) FROM public.game_editions WHERE is_active) = 1
                  )
            ),
            difficulty(name, internal_level) AS (
                VALUES
                    ('Mais Fácil', 1),
                    ('Padrão', 2),
                    ('Moderado', 3),
                    ('Difícil', 4),
                    ('Muito Difícil', 5),
                    ('Extremo', 6)
            )
            INSERT INTO public.ai_difficulties (id, game_edition_id, name, internal_level)
            SELECT md5(edition.id::text || ':ai-difficulty:' || difficulty.internal_level::text)::uuid,
                   edition.id,
                   difficulty.name,
                   difficulty.internal_level
            FROM target_editions AS edition
            CROSS JOIN difficulty
            ON CONFLICT (game_edition_id, name)
            DO UPDATE SET internal_level = EXCLUDED.internal_level;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("UPDATE public.ai_difficulties SET internal_level = 5 WHERE internal_level = 6;");

        migrationBuilder.DropCheckConstraint(
            name: "ck_ai_difficulties_internal_level",
            schema: "public",
            table: "ai_difficulties");

        migrationBuilder.AddCheckConstraint(
            name: "ck_ai_difficulties_internal_level",
            schema: "public",
            table: "ai_difficulties",
            sql: "internal_level BETWEEN 1 AND 5");
    }
}
