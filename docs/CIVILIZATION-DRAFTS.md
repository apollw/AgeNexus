# Draft de civilizações

O draft é uma sala compartilhável independente do cadastro de partidas. Concluir um draft não cria nem altera uma partida no AgeNexus.

## Fluxo

1. O administrador informa mapa, tipo de jogo, duas equipes equilibradas, seus jogadores e um representante por equipe.
2. Somente os dois representantes abrem o link e confirmam presença. Eles conversam com suas equipes fora do site antes de cada ação.
3. Os representantes alternam os bans. Cada equipe possui um ban por jogador: 1x1 gera um ban por lado, 2x2 gera dois e assim por diante até 4x4.
4. Após todos os bans, os representantes alternam os picks na mesma quantidade. Em cada pick, o representante indica qual jogador da equipe receberá a civilização.
5. A sala exibe o catálogo completo e atualiza imediatamente quais civilizações continuam disponíveis, quais foram banidas e quais foram escolhidas.
6. Todo ban ou pick exige uma confirmação antes de ser enviado ao servidor.
7. Ao final do último pick, todos veem o aviso de draft finalizado e a sala libera uma imagem PNG para compartilhamento.

O servidor valida a identidade do representante, a equipe, a fase, a vez, o jogador destinatário e a disponibilidade da civilização antes de aceitar cada ação. O estado é persistido no PostgreSQL e as telas conectadas recebem a atualização imediatamente pelo circuito do Blazor Server.

## Catálogo

O catálogo do Age of Empires II: Definitive Edition possui 62 civilizações, incluindo Dinamarqueses, Saxões e Varangianos. A sincronização idempotente é executada após as migrações no início da aplicação em produção.

## Escala

As notificações em memória atendem a implantação atual de uma instância no Render. Se a aplicação passar a executar várias instâncias simultâneas, o canal de notificações deve ser distribuído com Redis ou outro backplane; o PostgreSQL já permanece como fonte de verdade.
