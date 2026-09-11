# Experiência mobile

## Comportamento entregue

As melhorias de navegação e leitura foram integradas à `master` pelo [PR #27](https://github.com/apollw/AgeNexus/pull/27), no commit `382cb217417cd5f6d3bf56a3686db0e9f601dcc1`.

- **Sair:** botão com ícone e texto na barra superior para usuários autenticados. O formulário usa POST em `/account/logout` e token antifalsificação. A regra mobile que ocultava o botão foi removida.
- **Navegação:** destaque hexagonal no item ativo e em Mais quando aberto. A barra inferior e o conteúdo reservam espaço para a área segura do aparelho.
- **Tabelas:** partidas, rankings, clãs e civilizações passam a cartões com legendas até 760 px. Confronto e ações podem ocupar toda a largura.
- **Recordes Gerais:** cartões em uma coluna, títulos e nomes com quebra de linha e valores reposicionados para facilitar a leitura.

## Manutenção

A estrutura permanece nos componentes Razor e os ajustes de tamanho ficam em `src/AgeNexus.Web/wwwroot/styles/90-responsive.css`, importado por último. As tabelas adaptáveis usam `mobile-card-table`, `data-label` e `data-mobile-wide`. Não remova rótulos ao acrescentar campos.

## Validação automatizada confirmada

O [CI da integração](https://github.com/apollw/AgeNexus/actions/runs/34384175859) terminou com sucesso: restore, build Release, testes e verificação do snapshot do EF Core. Essa validação não substitui a inspeção visual nem o teste de logout em uma sessão autenticada.

## Conferência da publicação em 09/09/2026

As páginas públicas de Partidas e Recordes Gerais abriram no navegador. Porém, o CSS servido em produção ainda correspondia à versão anterior ao PR #27. Portanto, a revisão visual das melhorias mobile e o logout autenticado permanecem pendentes de conferência após o deploy. O `render.yaml` prevê publicação da branch `master` após aprovação dos checks; o estado efetivo deve ser acompanhado no Render.

## Roteiro de conferência visual e funcional

1. Conferir larguras de 360, 390 e 760 px, além de desktop: ausência de cortes, nomes longos, valores grandes e conteúdo livre da barra inferior.
2. Em uma sessão autenticada, verificar Sair e confirmar que o logout encerra a sessão do AgeNexus.
3. Navegar entre as opções inferiores e abrir Mais: conferir destaque hexagonal e acesso às demais seções.
4. Conferir partidas, rankings, clãs e civilizações com dados: cada campo deve ter sua legenda e as ações devem caber no cartão.
5. Conferir Recordes Gerais: campeão, demais colocações e navegação entre categorias.
6. Conferir desktop para garantir que as tabelas continuem alinhadas.

Não é necessário alterar variáveis de ambiente nem criar migrações para essas melhorias.
