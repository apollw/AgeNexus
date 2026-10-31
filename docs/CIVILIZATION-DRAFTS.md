# Draft de civilizações

O draft é uma sala compartilhável independente do cadastro de partidas. Concluir um draft não cria nem altera uma partida no AgeNexus.

## Fluxo

1. O administrador informa mapa, tipo de jogo, duas equipes equilibradas, seus jogadores, um representante por equipe e, opcionalmente, se cada rodada começa por bans ou picks. O padrão é começar por bans.
2. Qualquer pessoa com o link pode acompanhar a sala sem cadastro. Somente os dois representantes autenticados podem confirmar presença e executar ações; eles conversam com suas equipes fora do site antes de cada escolha.
3. Depois da confirmação dos dois representantes, pedra, papel e tesoura define quem age primeiro. As escolhas ficam ocultas até ambos responderem; em caso de empate, uma nova rodada é aberta.
4. Se o draft começar por bans, cada rodada segue `ban da primeira equipe → ban da segunda → pick da primeira → pick da segunda`. Se começar por picks, a ordem é invertida. O ciclo se repete conforme o tamanho dos times: uma rodada no 1x1, duas no 2x2 e assim por diante até 4x4.
5. Cada pick pertence à equipe. O AgeNexus não vincula a civilização a um jogador específico; os integrantes decidem essa divisão entre si.
6. Cada turno de ban ou pick possui 1 minuto e 30 segundos. Se o representante não agir, o servidor seleciona aleatoriamente uma civilização ainda disponível e passa a vez.
7. A sala exibe o catálogo completo, informa quem realizou o último ban ou pick e identifica em cada civilização a equipe responsável pela ação.
8. Todo ban ou pick manual exige uma confirmação antes de ser enviado ao servidor.
9. Ao final da última ação, todos veem o aviso de draft finalizado e a sala libera uma imagem PNG para compartilhamento.

Durante bans e picks, um painel flutuante permanece visível enquanto o usuário percorre a lista de civilizações. Ele resume a ação atual, a equipe da vez, a última escolha e o tempo restante; no celular fica acima da navegação inferior e pode ser recolhido sem ocultar a etapa ou o relógio.

O servidor valida a identidade do representante, a equipe, o tipo da ação, a vez e a disponibilidade da civilização antes de aceitar cada escolha. A configuração inicial, o resultado do pedra, papel e tesoura, o prazo, a autoria e o histórico recente ficam persistidos no PostgreSQL; as telas conectadas recebem a atualização imediatamente pelo circuito do Blazor Server e também conferem periodicamente o vencimento do relógio.

## Catálogo

O catálogo do Age of Empires II: Definitive Edition possui 62 civilizações, incluindo Dinamarqueses, Saxões e Varangianos. A sincronização idempotente é executada após as migrações no início da aplicação em produção.

## Escala

As notificações em memória atendem a implantação atual de uma instância no Render. Se a aplicação passar a executar várias instâncias simultâneas, o canal de notificações deve ser distribuído com Redis ou outro backplane; o PostgreSQL já permanece como fonte de verdade.
