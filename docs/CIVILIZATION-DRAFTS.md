# Draft de civilizações

O draft é uma sala compartilhável independente do cadastro de partidas. Concluir um draft não cria nem altera uma partida no AgeNexus.

## Fluxo

1. O administrador informa mapa, tipo de jogo, duas equipes equilibradas, seus jogadores e um representante por equipe.
2. Somente os dois representantes abrem o link e confirmam presença. Eles conversam com suas equipes fora do site antes de cada ação.
3. Ao iniciar, o servidor sorteia qual equipe age primeiro. Cada rodada segue `ban da primeira equipe → ban da segunda → pick da primeira → pick da segunda`, repetindo conforme o tamanho dos times: uma rodada no 1x1, duas no 2x2 e assim por diante até 4x4.
4. Cada pick pertence à equipe. O AgeNexus não vincula a civilização a um jogador específico; os integrantes decidem essa divisão entre si.
5. Cada turno possui 60 segundos. Se o representante não agir, o servidor seleciona aleatoriamente uma civilização ainda disponível e passa a vez.
6. A sala exibe o catálogo completo, informa quem realizou o último ban ou pick e identifica em cada civilização a equipe responsável pela ação.
7. Todo ban ou pick manual exige uma confirmação antes de ser enviado ao servidor.
8. Ao final do último pick, todos veem o aviso de draft finalizado e a sala libera uma imagem PNG para compartilhamento.

Durante bans e picks, um painel flutuante permanece visível enquanto o usuário percorre a lista de civilizações. Ele resume a ação atual, a equipe da vez, a última escolha e o tempo restante; no celular fica acima da navegação inferior e pode ser recolhido sem ocultar a etapa ou o relógio.

O servidor valida a identidade do representante, a equipe, o tipo da ação, a vez e a disponibilidade da civilização antes de aceitar cada escolha. A ordem sorteada, o prazo, a autoria e o histórico recente ficam persistidos no PostgreSQL; as telas conectadas recebem a atualização imediatamente pelo circuito do Blazor Server e também conferem periodicamente o vencimento do relógio.

## Catálogo

O catálogo do Age of Empires II: Definitive Edition possui 62 civilizações, incluindo Dinamarqueses, Saxões e Varangianos. A sincronização idempotente é executada após as migrações no início da aplicação em produção.

## Escala

As notificações em memória atendem a implantação atual de uma instância no Render. Se a aplicação passar a executar várias instâncias simultâneas, o canal de notificações deve ser distribuído com Redis ou outro backplane; o PostgreSQL já permanece como fonte de verdade.
