# Age Nexus — Especificação funcional e técnica

## 1. Visão do produto

**Age Nexus** é uma plataforma web para registrar, comprovar e analisar partidas de jogos da série *Age of Empires*, começando por *Age of Empires II*. A aplicação será construída em C# com Blazor no front-end e ASP.NET Core no back-end.

O sistema deve funcionar ao mesmo tempo como histórico do grupo, rede de perfis, central de clãs e plataforma competitiva. Ele deverá registrar partidas humanas em qualquer composição — 1x1, 2x2, 3x3, 4x4, 2x1, 3x2 e outras —, partidas contra IA, evidências, comentários, civilizações, mapas, equipes e resultados. O modelo não pode ser amarrado ao Age II: jogos, edições, facções e regras serão dados configuráveis para permitir outros títulos da série e, no futuro, até jogos externos.

### Nome recomendado

**Age Nexus** é o nome de trabalho recomendado. “Age” preserva a origem e a identidade do projeto; “Nexus” comunica o ponto de encontro entre jogadores, partidas, clãs, estatísticas e diferentes jogos.

Alternativas: **Age Ledger**, **Age Chronicle**, **Age Arena** e **Age Dominion**.

Slogan opcional: **Toda partida deixa uma história.**

## 2. Princípios do domínio

1. Uma partida é o fato histórico central e não pode ser reduzida apenas a vencedores e perdedores.
2. Jogador, conta de acesso e participante de partida são conceitos diferentes. Deve ser possível registrar partidas antigas com pessoas que ainda não criaram conta e posteriormente vincular o perfil.
3. Clã não é equipe. Um clã é uma organização social; uma equipe competitiva é uma formação exata de jogadores.
4. Pontos de carreira não representam habilidade. O sistema deve separar progressão por atividade de rating competitivo.
5. Partidas contra IA não devem inflar o ranking competitivo humano.
6. Estatísticas sempre devem mostrar tamanho da amostra e nunca tratar duas partidas como evidência equivalente a cinquenta.
7. Toda alteração relevante em uma partida validada deve ser auditável e provocar o recálculo dos rankings e das estatísticas afetadas.

## 3. Escopo funcional

### 3.1 Perfis de jogadores

Cada jogador terá um perfil público com:

- nome de exibição, foto/avatar, biografia curta e localização opcional;
- identificadores externos opcionais, como Steam, Xbox e Discord;
- jogos e edições jogados;
- civilizações favoritas declaradas pelo jogador;
- civilizações mais utilizadas calculadas pelo sistema;
- histórico de partidas, vitórias, derrotas, empates e aproveitamento;
- ratings por modalidade e posição nos rankings;
- pontos de carreira, nível, conquistas e sequências;
- clã atual e histórico de clãs;
- parceiros mais frequentes, melhores duplas/equipes e adversários recorrentes;
- configurações de privacidade.

As civilizações favoritas declaradas não devem ser confundidas com as mais usadas. A primeira informação expressa preferência; a segunda é uma estatística.

### 3.2 Registro de partidas

Uma partida deverá armazenar:

- jogo e edição;
- data e hora aproximada ou exata;
- tipo: PvP, humanos contra IA, misto ou todos contra todos;
- status: rascunho, enviada, aguardando confirmação, confirmada, contestada, validada ou anulada;
- natureza: casual, ranqueada, torneio ou série;
- mapa, tamanho do mapa, conjunto de mapas e localização inicial, quando aplicável;
- versão/patch do jogo;
- velocidade, limite populacional, condição de vitória e demais configurações relevantes;
- duração;
- equipes e respectivos participantes;
- civilização/facção de cada participante;
- escolha manual, aleatória ou desconhecida da civilização;
- resultado por equipe e motivo do encerramento;
- observações e tags;
- evidências e comentários;
- autor do registro, datas de criação e alteração.

O modelo deve aceitar qualquer quantidade de equipes e participantes. Formatos como 3x2 não serão valores fixos de um enum: serão derivados das quantidades de participantes humanos em cada equipe. Um rótulo amigável, como `3x2`, será calculado.

### 3.3 Séries e torneios

Uma série agrupa várias partidas, por exemplo melhor de três ou melhor de cinco. Estatísticas de civilizações são calculadas por partida; a vitória da série é uma estatística adicional. O rating deve ser atualizado por partida para não esconder a informação de um placar 3–2, mas a interface pode mostrar a série como uma unidade.

Torneios não fazem parte do primeiro MVP, mas o modelo deve permitir associar uma partida ou série a uma competição futuramente.

### 3.4 Evidências, confirmação e contestação

Tipos de evidência:

- link de vídeo ou transmissão, incluindo YouTube;
- imagem ou captura de tela;
- arquivo de replay/gravação da partida;
- link externo;
- comentário descritivo.

Uma evidência não valida automaticamente o resultado. A política recomendada é:

| Situação | Histórico | Pontos de carreira | Rating competitivo |
| --- | ---: | ---: | ---: |
| Rascunho | Não | Não | Não |
| Enviada, sem confirmação | Sim, como pendente | Não | Não |
| Confirmada por representantes dos lados adversários | Sim | Sim | Sim |
| Validada por moderador ou replay confiável | Sim, com selo | Sim | Sim |
| Contestada | Sim, sinalizada | Congelados | Congelado |
| Anulada | Sim, apenas em auditoria | Não | Não |

Em partidas PvP, pelo menos um participante de cada lado deve confirmar o resultado, salvo validação administrativa. Em partidas contra IA, a comunidade poderá exigir imagem, vídeo ou replay para que a partida gere pontos. A regra deverá ser configurável.

Comentários devem aceitar discussão sem alterar o fato registrado. Correções usam uma ação própria, com motivo, autor e trilha de auditoria.

### 3.5 Clãs

O sistema de clãs permitirá:

- criar clã com nome, sigla, brasão, descrição e regras;
- solicitar entrada, receber convite, aceitar ou recusar;
- papéis de líder, oficial e membro;
- clã aberto, mediante aprovação ou apenas por convite;
- mural e histórico de membros;
- estatísticas agregadas dos integrantes;
- confrontos entre clãs;
- ranking de clãs.

O ranking de clãs não deve ser a simples soma dos pontos de todos os membros, pois clãs maiores teriam vantagem automática. A métrica recomendada usa a média conservadora dos melhores integrantes elegíveis, com limite configurável, mais resultados diretos entre clãs.

## 4. Sistema de pontuação e ratings

### 4.1 Por que existirão dois sistemas

O produto terá duas medidas independentes:

1. **Rating de habilidade:** sobe e desce conforme resultado, força dos adversários, equilíbrio das equipes e modalidade. É a base dos rankings de “melhor jogador” e “melhor equipe”.
2. **Pontos de carreira:** sempre positivos quando merecidos e usados para níveis, conquistas, participação e ranking histórico de carreira. Eles não definem quem joga melhor.

Sem essa separação, um jogador poderia ultrapassar outro apenas repetindo partidas fáceis ou jogando muito contra IA.

### 4.2 Escopos de rating

Cada jogador poderá possuir ratings independentes nos seguintes escopos:

- Geral PvP;
- 1x1;
- equipes equilibradas, com filtros específicos de 2x2, 3x3 e 4x4;
- equipes assimétricas, como 2x1 e 3x2;
- todos contra todos, em uma fase posterior;
- temporadas, além do rating histórico.

Partidas contra IA terão um **Índice de Domínio contra IA**, separado do rating PvP.

O rating inicial recomendado é **1000**. Jogadores com menos de 10 partidas no escopo aparecem como provisórios. O ranking público principal exige no mínimo 10 partidas ranqueadas e pelo menos 3 adversários distintos.

### 4.3 Cálculo do rating PvP

O MVP poderá usar um Elo por equipes com ajustes de incerteza. Toda operação deve gerar um evento de rating imutável, permitindo reconstrução completa.

Para cada equipe:

```text
ForçaEfetiva = média dos ratings dos integrantes
                + 240 × log2(quantidade de jogadores da equipe)
```

O segundo termo estima a vantagem numérica em partidas assimétricas. Com isso, em igualdade de rating individual, 2 jogadores contra 1 recebem aproximadamente 240 pontos de vantagem; 3 contra 2, aproximadamente 140; e 4 contra 3, aproximadamente 100. Esses valores devem ficar em configuração e ser calibrados com os dados reais do grupo.

A expectativa de vitória da equipe A é:

```text
E(A) = 1 / (1 + 10 ^ ((ForçaEfetivaB - ForçaEfetivaA) / 400))
```

A atualização individual é:

```text
Delta = K × PesoDaModalidade × (Resultado - Expectativa)
```

Onde `Resultado` vale 1 para vitória, 0,5 para empate e 0 para derrota.

Valores iniciais recomendados:

| Condição | K |
| --- | ---: |
| Primeiras 10 partidas no escopo | 40 |
| Jogador estabelecido | 24 |
| Jogador inativo por longo período | 28 até recuperar confiança |

| Modalidade | Peso no delta |
| --- | ---: |
| 1x1 | 1,00 |
| 2x2 | 0,90 |
| 3x3 | 0,85 |
| 4x4 | 0,80 |
| Equipes assimétricas | 0,75 |

O peso menor em partidas grandes não afirma que elas valem menos como experiência; apenas reconhece que o resultado individual é mais difícil de atribuir a uma única pessoa.

O delta deve ser arredondado somente ao final e limitado inicialmente a ±40 pontos por partida. Empates, desistências e desconexões devem seguir regras configuráveis. Uma partida não deve gerar rating enquanto estiver pendente ou contestada.

### Evolução futura do rating

Quando a base crescer, o Elo pode ser substituído por um modelo com rating e incerteza explícita, semelhante a Glicko ou OpenSkill. O domínio deve depender de uma interface (`IRatingCalculator`) para permitir essa troca sem alterar partidas ou perfis.

### 4.4 Rating de equipes fixas

Para definir a melhor equipe, o sistema criará uma identidade de formação a partir do jogo, modalidade e conjunto ordenado de jogadores. Assim, “Ana + Bruno” é uma equipe distinta de “Ana + Carlos”, independentemente do clã.

Cada formação terá:

- rating próprio;
- partidas, vitórias, derrotas e aproveitamento;
- civilizações e composições mais usadas;
- rating conservador para classificação;
- histórico de integrantes imutável por formação.

O ranking principal exibirá separadamente melhor dupla, trio e quarteto. Um ranking geral de formações poderá ordenar pelo rating conservador (`rating - penalidade de incerteza`) desde que a equipe tenha pelo menos 5 partidas. Isso evita que uma equipe apareça em primeiro após uma única vitória.

### 4.5 Pontos de carreira

Tabela inicial por jogador participante:

| Formato PvP | Vitória | Empate | Derrota válida |
| --- | ---: | ---: | ---: |
| 1x1 | 100 | 50 | 25 |
| 2x2 | 75 | 38 | 20 |
| 3x3 | 60 | 30 | 18 |
| 4x4 ou maior | 50 | 25 | 15 |

Esses valores evitam que uma única partida 4x4 distribua oito vezes a recompensa total de uma 1x1. A derrota recebe poucos pontos para reconhecer participação sem permitir progressão eficiente por derrotas deliberadas.

Em equipes assimétricas, usa-se a linha correspondente ao maior tamanho de equipe e aplicam-se multiplicadores:

```text
Diferença = abs(jogadoresDoMeuTime - jogadoresDoAdversário)

Vitória do lado menor: 1 + 0,25 × Diferença, limitado a 1,75
Vitória do lado maior: 1 - 0,15 × Diferença, com mínimo de 0,50
Derrota do lado menor: 1,00
Derrota do lado maior: 0,75
```

Exemplo: em uma partida 3x2, a base é a linha de 3x3. Cada integrante da dupla recebe `60 × 1,25 = 75` pontos se vencer. Cada integrante do trio recebe `60 × 0,85 = 51` pontos se vencer. O rating ainda fará o ajuste principal pela expectativa de vitória.

### Pontos contra IA

Cada jogo cadastrará seus níveis de dificuldade numa escala interna de 1 a 5. Para Age II, os nomes concretos das dificuldades serão mapeados a essa escala, sem codificá-los na regra geral.

| Nível interno da IA | Vitória base | Derrota válida |
| --- | ---: | ---: |
| 1 | 8 | 2 |
| 2 | 15 | 3 |
| 3 | 28 | 5 |
| 4 | 45 | 7 |
| 5 | 70 | 10 |

O desafio é ajustado pela proporção entre IAs e humanos:

```text
MultiplicadorDeDesafio = sqrt(quantidadeDeIAs / quantidadeDeHumanos)
```

O multiplicador deve ficar entre 0,50 e 2,00. Modificadores de handicap e regras especiais devem reduzir ou aumentar a pontuação por configuração explícita.

Para impedir repetição artificial, em cada temporada e para a mesma combinação aproximada de dificuldade, mapa, aliados e adversários:

- as 3 primeiras vitórias concedem 100% dos pontos;
- as 7 seguintes concedem 25%;
- as demais continuam no histórico, mas não concedem pontos.

O Índice de Domínio contra IA deve valorizar maior dificuldade comprovada, desvantagem numérica, variedade de mapas/civilizações e sequência, e não apenas quantidade bruta de partidas.

### 4.6 Rankings públicos

O sistema oferecerá:

- Ranking Geral Competitivo PvP;
- Ranking 1x1;
- Ranking de 2x2, 3x3 e 4x4;
- Ranking de partidas assimétricas;
- Melhor dupla, melhor trio e melhor quarteto;
- Ranking de formações geral;
- Ranking de carreira;
- Ranking de domínio contra IA;
- Ranking de clãs;
- Rankings por temporada;
- Rankings históricos.

O **Ranking Geral Competitivo** será atualizado por todas as partidas PvP elegíveis, usando os pesos de modalidade. Partidas contra IA não o alteram. O **Ranking de Carreira** inclui pontos PvP, IA e conquistas, mas deve ser rotulado claramente como progressão histórica, não habilidade.

Critérios de desempate recomendados: rating conservador, confronto direto quando aplicável, força média dos adversários, número de adversários distintos e quantidade de partidas validadas.

## 5. Estatísticas

### 5.1 Estatísticas de jogadores

- total de partidas por jogo, edição, período e modalidade;
- vitórias, derrotas, empates, aproveitamento e rating ao longo do tempo;
- forma recente nas últimas 5, 10 e 20 partidas;
- maiores sequências de vitórias e derrotas;
- desempenho por mapa, tamanho de equipe e posição inicial;
- desempenho como favorito e como azarão segundo a expectativa do rating;
- parceiros com maior número de jogos e melhor aproveitamento;
- adversários mais frequentes;
- “algoz”: adversário com maior domínio, respeitando amostra mínima;
- “presa favorita”: confronto favorável, respeitando amostra mínima;
- desempenho com e contra cada civilização;
- civilizações declaradas favoritas versus realmente mais usadas;
- diversidade de civilizações;
- desempenho por patch e período.

### 5.2 Estatísticas de civilizações/facções

- número de escolhas e taxa de escolha;
- número de vitórias e taxa de vitória;
- taxa de vitória ajustada pela força dos jogadores;
- popularidade por período, mapa, modalidade e faixa de rating;
- confrontos entre civilizações em uma matriz;
- confrontos mais frequentes;
- melhores e piores confrontos, com amostra mínima;
- desempenho em espelhos;
- civilizações mais usadas por jogador;
- melhores jogadores com cada civilização;
- combinações de civilizações mais usadas por equipe;
- sinergias de composições em 2x2, 3x3 e 4x4;
- civilizações mais escolhidas aleatoriamente e manualmente;
- tendências antes e depois de patches.

### 5.3 Estatísticas de equipes e clãs

- histórico completo da formação;
- aproveitamento e rating ao longo do tempo;
- melhores e piores confrontos de formações;
- parceiros com maior sinergia;
- ganho de desempenho do jogador com determinado parceiro;
- composições de civilizações mais usadas e mais vencedoras;
- confrontos entre clãs;
- participação de cada membro nos resultados do clã;
- mapas e modalidades de melhor desempenho.

### 5.4 Estatísticas gerais da comunidade

- partidas por dia, mês, temporada e jogo;
- distribuição dos formatos, incluindo 1x1, 2x2, 3x2 etc.;
- duração média e mediana;
- mapas mais usados;
- civilizações mais populares e vencedoras;
- confrontos mais recorrentes;
- equilíbrio entre lados maiores e menores em partidas assimétricas;
- dificuldade de IA mais enfrentada;
- recordes da comunidade;
- maior zebra segundo a expectativa de rating;
- maior sequência, maior rivalidade e equipe mais ativa.

### 5.5 Honestidade estatística

Toda taxa deve exibir o total de partidas (`n`). Rankings estatísticos de taxa de vitória devem exigir amostra mínima e aplicar suavização bayesiana ou intervalo de confiança. Uma civilização com 1 vitória em 1 partida não pode aparecer automaticamente acima de outra com 70 vitórias em 100.

Filtros mínimos: jogo, edição, temporada, intervalo de datas, patch, mapa, modalidade, tamanho de equipe, jogador, clã, civilização e status de validação.

Partidas anuladas são excluídas. Partidas pendentes podem aparecer apenas em visões de histórico, sempre identificadas, e não entram em rankings ou taxas oficiais.

## 6. Modelo de dados conceitual

### Identidade e perfis

- `ApplicationUser`: autenticação, e-mail e segurança.
- `PlayerProfile`: identidade pública do jogador.
- `ExternalIdentity`: Steam, Xbox, Discord e outros identificadores.
- `PlayerFavoriteFaction`: preferências declaradas.

### Catálogo de jogos

- `Game`: franquia ou jogo principal.
- `GameEdition`: Age II Definitive Edition, edição futura etc.
- `Faction`: civilização/facção vinculada à edição.
- `MapDefinition`: mapa e metadados.
- `GamePatch`: versão e período de vigência.
- `AiDifficulty`: nome exibido e nível interno de 1 a 5.
- `RulePreset`: configurações reutilizáveis de partida.

### Partidas

- `Match`: agregado principal.
- `MatchTeam`: um lado participante e seu resultado.
- `MatchParticipant`: jogador humano, convidado ou IA pertencente a uma equipe.
- `MatchSettings`: configurações específicas.
- `MatchSeries`: agrupamento de partidas.
- `MatchEvidence`: vídeo, imagem, replay ou link.
- `MatchConfirmation`: confirmação ou contestação por participante.
- `MatchComment`: discussão.
- `MatchRevision`: trilha de alterações.

### Social

- `Clan`;
- `ClanMembership`;
- `ClanInvitation`;
- `ClanJoinRequest`.

### Competição

- `Season`;
- `RatingScope`;
- `PlayerRating`;
- `RatingEvent`;
- `TeamLineup`;
- `TeamLineupMember`;
- `TeamRating`;
- `CareerPointEvent`;
- `Achievement` e `PlayerAchievement`.

### Estatísticas

As tabelas de partidas são a fonte da verdade. Projeções como `PlayerStats`, `FactionStats`, `MatchupStats`, `TeamStats` e `ClanStats` podem ser materializadas para leitura rápida, mas devem ser reconstruíveis.

### Decisões importantes de modelagem

- Usar identificadores estáveis, preferencialmente `Guid` ou `Ulid`.
- Guardar horários em UTC e converter na interface.
- Não armazenar `2x2` como verdade primária; derivar o formato das equipes.
- Representar IA como participante tipado, não como usuário falso.
- Guardar o resultado na equipe; o resultado individual é derivado.
- Não codificar civilizações ou dificuldades em enums do C#.
- Rating e pontos são livros contábeis de eventos, não apenas números sobrescritos.
- Usar exclusão lógica e auditoria para registros competitivos.

## 7. Arquitetura recomendada

### 7.1 Estilo

Começar como **monólito modular**, evitando a complexidade prematura de microsserviços. A solução pode ser separada em módulos:

- Identity;
- Players;
- GameCatalog;
- Matches;
- EvidenceAndModeration;
- Ratings;
- Statistics;
- Clans;
- Achievements.

Cada módulo deve concentrar suas regras de domínio, casos de uso e persistência, comunicando eventos internos quando necessário.

### 7.2 Tecnologias

- Blazor Web App com interatividade adequada às páginas;
- ASP.NET Core no back-end;
- Entity Framework Core;
- PostgreSQL como banco relacional recomendado;
- ASP.NET Core Identity para autenticação e autorização;
- armazenamento de objetos para imagens e replays no ambiente de produção;
- links externos armazenados como metadados, sem copiar vídeos do YouTube;
- biblioteca de gráficos compatível com Blazor, escolhida na implementação;
- suíte de testes com xUnit ou equivalente do ecossistema .NET.

Usar a versão LTS do .NET suportada no início efetivo da implementação. A decisão de hospedagem fica fora desta especificação.

### 7.3 Camadas por módulo

```text
Domain: entidades, valores, invariantes e eventos
Application: comandos, consultas, DTOs, validação e autorização de caso de uso
Infrastructure: EF Core, arquivos, identidade e integrações
Web: componentes Blazor, endpoints e composição da aplicação
```

Não é necessário criar uma API separada para tudo no primeiro momento, mas os casos de uso não devem ficar dentro dos componentes Blazor. Isso permite introduzir aplicativos móveis ou API pública no futuro.

### 7.4 Consistência e recálculo

Quando uma partida é validada:

1. o estado validado é persistido;
2. um evento interno `MatchValidated` é registrado;
3. ratings e pontos recebem eventos idempotentes;
4. projeções estatísticas são atualizadas;
5. perfis e rankings passam a refletir o resultado.

Quando uma partida é corrigida ou anulada, os eventos anteriores são revertidos ou todo o escopo afetado é reconstruído em ordem cronológica. Um `MatchId` não pode gerar o mesmo evento de pontuação duas vezes.

### 7.5 Segurança e papéis

Papéis globais:

- visitante;
- jogador autenticado;
- moderador;
- administrador.

Papéis de clã são independentes. Uploads devem validar extensão, tipo real, tamanho e autorização. Links externos devem ser sanitizados. Comentários e descrições precisam de proteção contra conteúdo malicioso. Toda ação de moderação deve registrar autor, data e motivo.

## 8. Páginas principais

- início com atividade recente, líderes e destaques;
- explorar partidas;
- detalhes da partida com equipes, civilizações, provas e comentários;
- registrar/editar partida;
- central de confirmações e contestações;
- perfil do jogador;
- comparação entre dois jogadores;
- página de formação/equipe;
- rankings;
- estatísticas gerais;
- explorador de civilizações e matriz de confrontos;
- clãs e página do clã;
- temporadas;
- administração do catálogo de jogos;
- fila de moderação.

## 9. Casos de uso prioritários

1. Criar conta e perfil.
2. Cadastrar um jogador histórico sem conta.
3. Registrar uma partida 1x1.
4. Registrar uma partida com equipes de tamanhos iguais ou diferentes.
5. Registrar humanos contra uma ou mais IAs.
6. Adicionar link, imagem ou replay como evidência.
7. Solicitar confirmação aos demais participantes.
8. Confirmar, contestar, corrigir e validar partida.
9. Consultar histórico e confronto direto.
10. Consultar rankings por modalidade.
11. Consultar estatísticas de civilizações.
12. Criar clã e administrar membros.

## 10. Roadmap sugerido

### Fase 1 — Fundação

Solução .NET, autenticação, perfis, catálogo de jogos/edições/civilizações/mapas, autorização e banco de dados.

### Fase 2 — Núcleo de partidas

Registro flexível de equipes, participantes humanos/IA, resultados, evidências por link, confirmação e histórico básico.

### Fase 3 — Competição

Rating geral e por modalidade, pontos de carreira, formações fixas, temporadas e rankings.

### Fase 4 — Estatísticas

Painéis de jogadores, confrontos diretos, civilizações, mapas, composições e evolução temporal. Introduzir projeções materializadas e recálculo.

### Fase 5 — Comunidade

Clãs, convites, pedidos de entrada, comentários, conquistas e recordes.

### Fase 6 — Expansão

Upload de replays, eventual leitura automática de metadados, torneios, API pública e novos jogos.

## 11. Escopo mínimo do MVP

O MVP deve conter:

- cadastro e login;
- perfis de jogadores, inclusive perfis históricos sem conta;
- Age of Empires II como primeiro jogo configurado;
- civilizações e mapas administráveis;
- partidas PvP e contra IA com equipes arbitrárias;
- links e imagens como evidências;
- confirmação dos participantes;
- histórico, confronto direto e filtros;
- rating Geral PvP, 1x1 e Equipes;
- pontos de carreira;
- ranking de jogadores e formações;
- estatísticas essenciais de civilizações;
- trilha de auditoria básica.

Clãs podem entrar no fim do MVP ou na primeira versão posterior, pois não bloqueiam o núcleo histórico e competitivo.

## 12. Critérios de aceite essenciais

- Deve ser possível registrar 1x1, 2x2, 3x3, 4x4, 2x1 e 3x2 sem alterar o código.
- Uma partida pode misturar perfis com conta, perfis históricos e IA.
- Nenhuma partida pendente ou contestada altera rating oficial.
- Confirmar duas vezes a mesma partida não duplica rating ou pontos.
- Anular uma partida remove seus efeitos derivados de modo auditável.
- Partidas contra IA nunca alteram o rating competitivo PvP.
- A formação exata de jogadores possui estatística e rating próprios.
- Clã e formação competitiva permanecem conceitos distintos.
- Todas as taxas exibem tamanho de amostra.
- Filtros por período, modalidade, jogador, civilização e mapa funcionam em histórico e estatísticas.
- Civilizações, mapas, jogos e dificuldades são dados configuráveis.
- O sistema consegue recalcular ratings e estatísticas a partir do histórico validado.

## 13. Instruções para a IA implementadora

1. Trate este documento como a especificação funcional inicial, não como autorização para implementar todas as fases de uma vez.
2. Comece pelo monólito modular e pelo modelo de domínio; não crie microsserviços.
3. Antes de codificar, produza a estrutura da solução, o diagrama conceitual das entidades e as decisões registradas como ADRs.
4. Não coloque regras de rating, confirmação ou estatísticas em componentes Blazor.
5. Modele partidas por equipes e participantes, sem enums rígidos para formatos como 2x2.
6. Implemente `IRatingCalculator`, `ICareerPointCalculator` e serviços de reconstrução de projeções.
7. Mantenha rating PvP, domínio contra IA e pontos de carreira separados.
8. Faça eventos de rating e pontos idempotentes e auditáveis.
9. Cubra primeiro com testes as partidas assimétricas, confirmação concorrente, anulação, recálculo e prevenção de duplicidade.
10. Use migrações do EF Core e dados iniciais configuráveis para o primeiro jogo.
11. Entregue cada fase verticalmente: domínio, persistência, casos de uso, interface e testes.
12. Não decida hospedagem nesta etapa.

## 14. Questões que podem ser decididas depois

- o nome definitivo e identidade visual;
- se perfis serão públicos por padrão;
- política exata de evidência para partidas contra IA;
- duração e reinício das temporadas;
- regras para desconexão e substituição de jogador;
- suporte inicial a todos contra todos;
- limite e tamanho de uploads;
- critérios exatos do ranking de clãs;
- quais estatísticas avançadas entram no primeiro lançamento;
- provedor de hospedagem e armazenamento.

Essas decisões não impedem a construção correta do núcleo, desde que permaneçam configuráveis e não sejam codificadas como suposições permanentes.
