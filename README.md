# Age Nexus

Plataforma para registrar, comprovar e analisar partidas da série *Age of Empires*.

O núcleo competitivo já inclui catálogo multi-jogo, partidas PvP/PvE/híbridas, comprovação, confirmação e moderação, regras versionadas de rating e carreira, formações, clãs, rankings e estatísticas de civilizações. Consulte a estrutura em [`docs/architecture`](docs/architecture/solution-structure.md), a especificação em [`Age-Nexus-Specs.md`](Age-Nexus-Specs.md) e as decisões em [`docs/adr`](docs/adr/0001-modular-monolith.md).

Para configurar o ambiente e iniciar a aplicação, consulte [Como rodar o Age Nexus](docs/COMO-RODAR.md).

Para banco PostgreSQL, autenticação e armazenamento no Supabase, consulte [Supabase no Age Nexus](docs/SUPABASE.md).

Para alterar o schema e aplicar migrações PostgreSQL, consulte [EF Core e PostgreSQL](docs/EF-CORE.md).

Para importar replays e entender o cálculo de MVP, consulte [Estatísticas pós-jogo e replays](docs/REPLAY-ANALYSIS.md).

```powershell
dotnet build AgeNexus.slnx
dotnet test AgeNexus.slnx
dotnet run --project src/AgeNexus.Web
```
