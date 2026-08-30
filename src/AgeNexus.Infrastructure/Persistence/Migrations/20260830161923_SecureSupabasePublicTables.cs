using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SecureSupabasePublicTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE public.player_profiles ENABLE ROW LEVEL SECURITY;
                ALTER TABLE public.ai_difficulties ENABLE ROW LEVEL SECURITY;
                ALTER TABLE public.matches ENABLE ROW LEVEL SECURITY;
                ALTER TABLE public.match_teams ENABLE ROW LEVEL SECURITY;
                ALTER TABLE public.match_participants ENABLE ROW LEVEL SECURITY;

                REVOKE ALL ON TABLE public.player_profiles FROM anon, authenticated;
                REVOKE ALL ON TABLE public.ai_difficulties FROM anon, authenticated;
                REVOKE ALL ON TABLE public.matches FROM anon, authenticated;
                REVOKE ALL ON TABLE public.match_teams FROM anon, authenticated;
                REVOKE ALL ON TABLE public.match_participants FROM anon, authenticated;

                ALTER DEFAULT PRIVILEGES FOR ROLE postgres IN SCHEMA public
                    REVOKE ALL ON TABLES FROM anon, authenticated;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER DEFAULT PRIVILEGES FOR ROLE postgres IN SCHEMA public
                    GRANT ALL ON TABLES TO anon, authenticated;

                GRANT ALL ON TABLE public.player_profiles TO anon, authenticated;
                GRANT ALL ON TABLE public.ai_difficulties TO anon, authenticated;
                GRANT ALL ON TABLE public.matches TO anon, authenticated;
                GRANT ALL ON TABLE public.match_teams TO anon, authenticated;
                GRANT ALL ON TABLE public.match_participants TO anon, authenticated;

                ALTER TABLE public.player_profiles DISABLE ROW LEVEL SECURITY;
                ALTER TABLE public.ai_difficulties DISABLE ROW LEVEL SECURITY;
                ALTER TABLE public.matches DISABLE ROW LEVEL SECURITY;
                ALTER TABLE public.match_teams DISABLE ROW LEVEL SECURITY;
                ALTER TABLE public.match_participants DISABLE ROW LEVEL SECURITY;
                """);
        }
    }
}
