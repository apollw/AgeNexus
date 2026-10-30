# Draft de civilizações

O draft é uma sala compartilhável independente do cadastro de partidas. Concluir um draft não cria nem altera uma partida no AgeNexus.

## Fluxo

1. Um usuário autenticado informa mapa, tipo de jogo, equipes e de dois a oito jogadores.
2. Cada jogador abre o link usando sua conta vinculada e confirma presença.
3. Em uma fila alternada entre as equipes, cada participante reserva secretamente duas civilizações do conjunto rotativo apresentado.
4. Na mesma ordem, cada participante faz um ban global sem ver as preferências dos demais. Se uma das duas preferências de alguém for banida, a outra é protegida e confirmada automaticamente.
5. Quem ainda possui duas opções escolhe uma delas na fase de picks.
6. Todos conferem e confirmam o resultado. A sala então libera uma imagem PNG para compartilhamento.

O servidor valida a identidade, a fase e a vez antes de aceitar cada ação. O estado é persistido no PostgreSQL e as telas conectadas recebem a atualização imediatamente pelo circuito do Blazor Server.

## Rodízio

Cada sala recebe apenas parte do catálogo. Civilizações menos utilizadas em drafts anteriores têm prioridade no próximo conjunto. Civilizações banidas não contam como utilizadas e, portanto, retornam ao rodízio. Isso evita mostrar sempre toda a lista sem fazer opções desaparecerem definitivamente.

## Catálogo

O catálogo do Age of Empires II: Definitive Edition possui 62 civilizações, incluindo Dinamarqueses, Saxões e Varangianos. A sincronização idempotente é executada após as migrações no início da aplicação em produção.

## Escala

As notificações em memória atendem a implantação atual de uma instância no Render. Se a aplicação passar a executar várias instâncias simultâneas, o canal de notificações deve ser distribuído com Redis ou outro backplane; o PostgreSQL já permanece como fonte de verdade.
