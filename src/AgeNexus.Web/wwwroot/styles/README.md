# Organização dos estilos

`app.css` é apenas o ponto de entrada. Os arquivos são importados nesta ordem:

- `00-foundation.css`: variáveis de cor, reset e padrões do documento.
- `10-shell.css`: estrutura principal, sidebar, navegação e topbar.
- `20-components.css`: botões, painéis, títulos e componentes compartilhados.
- `30-dashboard.css`: página inicial, partidas recentes e ações rápidas.
- `40-data-pages.css`: tabelas, jogadores e rankings.
- `50-forms-performance.css`: autenticação, formulários e desempenho das partidas.
- `60-records.css`: página de Recordes Gerais.
- `70-profile-catalog.css`: perfil e configuração do catálogo.
- `90-responsive.css`: todos os ajustes de breakpoints; deve permanecer por último.

Ao criar uma página nova, prefira um arquivo próprio com prefixo numérico. Classes específicas de página devem usar um prefixo, como `records-` ou `home-`, para evitar colisões.

## Escala tipográfica

Os tamanhos compartilhados ficam centralizados em `00-foundation.css`:

- `--font-xs` e `--font-sm`: selos e informações secundárias.
- `--font-caption`: legendas, metadados e cabeçalhos de tabela.
- `--font-copy`: conteúdo compacto de cards, tabelas e formulários.
- `--font-body`: texto geral da aplicação.
- `--font-control`: botões e controles em destaque.
- `--font-lead`: textos introdutórios.

Prefira esses tokens a novos valores fixos. Títulos podem manter tamanhos próprios quando fizerem parte da hierarquia visual da página.

## Comportamento mobile

O breakpoint de navegação e cartões é `max-width: 760px`, em `90-responsive.css`.

- `.logout-button` deve permanecer visível para usuários autenticados.
- O destaque da navegação inferior é desenhado em `::before` com `clip-path` hexagonal; evite reintroduzir fundos retangulares no item ativo.
- `.mobile-card-table` converte linhas em cartões. Cada célula usa `data-label` como legenda; `data-mobile-wide` ocupa as duas colunas.
- Tabelas sem essa classe mantêm rolagem horizontal no mobile.
- A navegação inferior e o conteúdo usam `env(safe-area-inset-bottom)` para acomodar dispositivos com área segura.
- Recordes Gerais têm ajustes próprios para nomes, valores, campeões e demais colocações.

Veja o [roteiro de conferência mobile](../../../../docs/MOBILE-UX.md).
