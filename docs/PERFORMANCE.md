# Diagnóstico de lentidão no cadastro de partidas

A revisão do código identificou uma ineficiência: salvar partidas ou estatísticas invalidava também o catálogo usado no cadastro. O próximo acesso repetia sete consultas sequenciais de edições, jogadores, facções, mapas, dificuldades, temporadas e patches. O catálogo agora tem cache independente (expiração de dois minutos), invalidado por mudanças de jogadores, temporadas e catálogo. Histórico e rankings continuam sendo invalidados por alterações de domínio.

A listagem já limita o resultado antes de buscar participantes. As estatísticas são filtradas pela partida/relatório; não foi identificada leitura de todo o histórico nesse formulário. Sete partidas, por si só, não explicam uma degradação grave. A correção remove trabalho desnecessário, mas não comprova a causa completa observada em produção.

O histórico público usa paginação no servidor com 10 partidas por página. Filtros de período e jogador são aplicados antes de `Count`, `Skip` e `Take`; participantes e evidências são consultados somente para os identificadores da página atual. A tela de detalhes carrega as estatísticas de uma única partida e não expõe relatórios incompletos.

Diretórios e tabelas extensíveis também são apresentados em páginas: jogadores e civilizações exibem 12 itens, clãs 15, rankings 10 e o elenco administrativo 10 por vez. Rankings carregam apenas o quadro selecionado. Listas naturalmente limitadas — participantes de uma partida, evidências e líderes resumidos — não recebem controles desnecessários.

Navegações, envios de formulário e operações interativas demoradas exibem uma sobreposição padronizada com animação de carregamento, bloqueando novos comandos até a conclusão. A extração de capturas é a exceção: sua barra de progresso detalhada continua sendo o único indicador durante o OCR.

## Verificação após deploy

Compare abrir `/partidas`, abrir o cadastro, salvar uma partida e abrir/preencher as estatísticas. Registre duração aproximada e horário UTC, sem dados pessoais. Não crie partidas fictícias em produção apenas para testar.

O servidor emite avisos a partir de 500 ms:

- `Slow query group`: tempo total da consulta em cache e espera pela trava; inclui obtenção dos dados.
- `Slow statistics load`: duração completa da leitura das estatísticas.
- `Slow database command`: duração de execução de comando SQL; não inclui necessariamente conexão e consumo do resultado.

Os novos avisos não registram SQL, parâmetros, identificadores de jogadores ou credenciais. O TraceId ajuda a correlacionar quando há uma Activity ativa, mas pode estar ausente em eventos Blazor. Os tempos não representam toda a latência do navegador/SignalR.

Se persistir, correlacione os avisos e erros do EF com CPU/memória do Render, região e conexões do Supabase e desconexões SignalR. Tempo alto apenas no formulário durante digitação exige medir navegador e circuito Blazor. Não houve acesso a métricas do servidor ou banco hospedado nesta revisão, nem benchmark de produção. Os testes de regressão verificam invalidação e reutilização após oito gravações, não desempenho de PostgreSQL.

## Recuperação da página de desempenho

A página de desempenho usa uma única sequência de consultas para não multiplicar conexões PostgreSQL. O carregamento principal é limitado a 30 segundos e a galeria de evidências a 15 segundos. Falhas, partidas ausentes e contas sem perfil vinculado agora encerram o indicador de carregamento e apresentam uma ação útil. O botão de nova tentativa repete apenas a leitura da página. Exceções completas permanecem nos logs do servidor.

## Incidente de setembro de 2026

Com aproximadamente sete partidas registradas, foi observado aumento progressivo no tempo para abrir a listagem, cadastrar uma partida e carregar a tela de desempenho. A tela de desempenho chegou a permanecer indefinidamente em `Carregando partida...`.

A análise encontrou duas causas concretas no código:

- toda gravação invalidava também o cache do catálogo estável do formulário, forçando sete consultas adicionais no acesso seguinte;
- o relatório de desempenho podia abrir até seis contextos de banco simultaneamente, além da leitura paralela do perfil, aumentando a disputa pelo limite de conexões do PostgreSQL hospedado.

As correções foram integradas pelos PRs #33 e #34. O catálogo passou a ter invalidação própria e a leitura do relatório agora usa uma sequência controlada em um único contexto. A interface encerra o carregamento em até 30 segundos, mostra erro e permite nova tentativa. A galeria de evidências possui limite independente de 15 segundos.

### Lição de validação

Build e testes automatizados confirmam regras e regressões conhecidas, mas não reproduzem sozinhos latência de rede, limites do plano hospedado, crescimento real dos dados e uso contínuo do Blazor Server. Mudanças em caminhos críticos devem combinar testes com uma verificação manual após o deploy. Relatos de aumento progressivo, horário aproximado e tela afetada são evidências de diagnóstico e devem ser correlacionados com os avisos de desempenho do servidor.

## Avatares

As listagens usam um componente único com dimensões reservadas, decodificação assíncrona e carregamento tardio para imagens fora da área visível. Avatares locais possuem URL versionada pelo conteúdo, cache imutável no navegador por um ano e cache de seis horas em memória no servidor. Assim, navegar entre jogadores, rankings e recordes não repete uma leitura do PostgreSQL para cada foto. O cache em memória aceita apenas a versão hexadecimal presente na URL e contabiliza o tamanho das imagens no limite global da aplicação.

## Navegação pelos recordes

Marcas positivas e negativas são classificadas nos dados e exibidas separadamente no perfil e em Recordes Gerais. As categorias permanecem recolhidas até serem solicitadas; somente uma fica visível por vez e cada categoria aberta é mantida na memória do componente. No perfil, a consulta das marcas é adiada até o primeiro clique. A consulta geral também utiliza o cache compartilhado de competição, com validade de dois minutos e invalidação após alterações relevantes.
