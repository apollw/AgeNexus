# Age Nexus

Plataforma para registrar, comprovar e analisar partidas da série *Age of Empires*.

O núcleo competitivo já inclui catálogo multi-jogo, partidas PvP/PvE/híbridas, comprovação por vídeo e capturas, confirmação e moderação, regras versionadas de rating e carreira, formações, clãs, rankings e estatísticas de civilizações. A autenticação é exclusivamente pelo Google; jogadores vinculam nicks históricos com aprovação administrativa e personalizam perfis públicos com foto e civilização favorita. Consulte a estrutura em [`docs/architecture`](docs/architecture/solution-structure.md), a especificação em [`Age-Nexus-Specs.md`](Age-Nexus-Specs.md) e as decisões em [`docs/adr`](docs/adr/0001-modular-monolith.md).

Para configurar o ambiente e iniciar a aplicação, consulte [Como rodar o Age Nexus](docs/COMO-RODAR.md).

Para banco PostgreSQL, autenticação e armazenamento no Supabase, consulte [Supabase no Age Nexus](docs/SUPABASE.md).

Para alterar o schema e aplicar migrações PostgreSQL, consulte [EF Core e PostgreSQL](docs/EF-CORE.md).

Para importar replays e entender o cálculo de MVP, consulte [Estatísticas pós-jogo e replays](docs/REPLAY-ANALYSIS.md).

Para publicar a aplicação, consulte [Publicação no Render](docs/RENDER.md).

Os emblemas de civilizações têm origem no projeto comunitário [AoE2 Tech Tree](https://github.com/SiegeEngineers/aoe2techtree) e são utilizados conforme as [Game Content Usage Rules da Microsoft](https://www.xbox.com/en-US/developers/rules). Age of Empires II © Microsoft Corporation. O AgeNexus não é endossado nem afiliado à Microsoft.

```powershell
dotnet build AgeNexus.slnx
dotnet test AgeNexus.slnx
dotnet run --project src/AgeNexus.Web
```

### Guia de pontuação

A página pública `/pontuacao`, acessível pelos rankings e pelo menu (em **Mais** no celular), explica rating competitivo, carreira, IA, MVP indicado pelo administrador e classificações provisórias. Os valores da tabela PvP vêm de `ScoringRuleSet`. Detalhes adicionais ficam em um bloco expansível. O texto descreve as regras implementadas, incluindo o contador atual de repetição PvE e a ausência de geração automática de pontos de clã.
