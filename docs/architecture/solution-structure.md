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

Os namespaces serão subdivididos por módulo: `Identity`, `Players`, `GameCatalog`, `Matches`, `EvidenceAndModeration`, `Ratings`, `Statistics`, `Clans` e `Achievements`. Web e Infrastructure dependem de Application; Application depende de Domain; Domain não depende das demais camadas.

A primeira fatia cobre catálogo mínimo e criação de partidas com equipes arbitrárias. Persistência EF Core, identidade e telas entram depois de estabilizadas as invariantes do agregado `Match`.
