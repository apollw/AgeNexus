# Diagnóstico de lentidão no cadastro de partidas

A revisão do código identificou uma ineficiência: salvar partidas ou estatísticas invalidava também o catálogo usado no cadastro. O próximo acesso repetia sete consultas sequenciais de edições, jogadores, facções, mapas, dificuldades, temporadas e patches. O catálogo agora tem cache independente (expiração de dois minutos), invalidado por mudanças de jogadores, temporadas e catálogo. Histórico e rankings continuam sendo invalidados por alterações de domínio.

A listagem já limita o resultado antes de buscar participantes. As estatísticas são filtradas pela partida/relatório; não foi identificada leitura de todo o histórico nesse formulário. Sete partidas, por si só, não explicam uma degradação grave. A correção remove trabalho desnecessário, mas não comprova a causa completa observada em produção.

## Verificação após deploy

Compare abrir `/partidas`, abrir o cadastro, salvar uma partida e abrir/preencher as estatísticas. Registre duração aproximada e horário UTC, sem dados pessoais. Não crie partidas fictícias em produção apenas para testar.

O servidor emite avisos a partir de 500 ms:

- `Slow query group`: tempo total da consulta em cache e espera pela trava; inclui obtenção dos dados.
- `Slow statistics load`: duração completa da leitura das estatísticas.
- `Slow database command`: duração de execução de comando SQL; não inclui necessariamente conexão e consumo do resultado.

Os novos avisos não registram SQL, parâmetros, identificadores de jogadores ou credenciais. O TraceId ajuda a correlacionar quando há uma Activity ativa, mas pode estar ausente em eventos Blazor. Os tempos não representam toda a latência do navegador/SignalR.

Se persistir, correlacione os avisos e erros do EF com CPU/memória do Render, região e conexões do Supabase e desconexões SignalR. Tempo alto apenas no formulário durante digitação exige medir navegador e circuito Blazor. Não houve acesso a métricas do servidor ou banco hospedado nesta revisão, nem benchmark de produção. Os testes de regressão verificam invalidação e reutilização após oito gravações, não desempenho de PostgreSQL.
