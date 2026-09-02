# Estrutura inicial da solução

Age Nexus começa como um monólito modular. Os limites funcionais vivem em módulos; os projetos representam as camadas compartilhadas enquanto o produto ainda é pequeno.

```text
src/
  AgeNexus.Domain/          entidades, valores, invariantes e eventos por módulo
  AgeNexus.Application/     casos de uso, contratos, DTOs e portas
  AgeNexus.Infrastructure/  persistência, identidade, arquivos e integrações
  AgeNexus.Web/             composição ASP.NET Core e interface Blazor
tests/
  AgeNexus.Domain.Tests/    testes rápidos das regras centrais
```

Os namespaces serão subdivididos por módulo: `Identity`, `Players`, `GameCatalog`, `Matches`, `MatchPerformance`, `EvidenceAndModeration`, `Ratings`, `Statistics`, `Clans` e `Achievements`. Web e Infrastructure dependem de Application; Application depende de Domain; Domain não depende das demais camadas.

O núcleo atual cobre catálogo configurável, identidade, partidas com equipes arbitrárias, evidências e decisões de verificação, relatórios pós-jogo por replay ou entrada manual, desempenho/MVP, clãs, livros imutáveis de rating/pontos, consultas de rankings e estatísticas e telas Blazor iniciais. Os fluxos transacionais ficam em `Infrastructure`, implementando contratos de `Application`; as fórmulas puras permanecem em `Application` e as invariantes em `Domain`.
