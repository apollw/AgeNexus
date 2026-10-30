# Estatísticas pós-jogo e replays

O relatório pós-jogo e o replay são recursos independentes. As estatísticas são preenchidas manualmente ou importadas do JSON do AgeExtractor. O replay é somente o arquivo original da partida disponibilizado para download.

O JSON do AgeExtractor é processado temporariamente para preencher o formulário e depois descartado. O banco recebe as estatísticas normalizadas e apenas um resumo da importação, nunca o documento JSON completo.

## Formatos e armazenamento

O upload aceita `.aoe2record`, `.mgz` e `.mgx`, com limite de 50 MB. O arquivo é armazenado no bucket público de evidências do Supabase Storage e não é interpretado pela aplicação.

## Fluxo

1. O administrador seleciona o replay na tela de desempenho.
2. O servidor valida extensão e tamanho, calcula o SHA-256 e envia o arquivo ao Supabase Storage.
3. Um novo upload substitui o replay anterior da mesma partida.
4. A página pública de evidências e o histórico de partidas disponibilizam o download.
5. As estatísticas continuam sendo preenchidas manualmente ou pelo JSON, sem qualquer alteração causada pelo replay.

## Integridade e limitações

- O SHA-256 impede reutilização silenciosa do mesmo replay em mais de uma partida.
- O nome original é preservado para o download.
- Excluir uma evidência ou a própria partida também solicita a remoção do arquivo no Storage.
- Dados manuais e transcritos de capturas seguem o mesmo fluxo de confirmação das equipes.
- Relatórios contestados não geram bônus.
- O bônus nunca altera rating competitivo.
- Partidas exclusivamente entre humanos e IA não são mais registradas; o tipo permanece apenas para compatibilidade com dados antigos.

## Fórmula de desempenho

As quatro pontuações finais oficiais são normalizadas entre os humanos da mesma partida. Em 1x1, os pesos são militar 45%, economia 35%, tecnologia 10% e sociedade 10%. Em jogos com equipes humanas, são 40%, 30%, 10% e 20%, respectivamente. O MVP é informado por equipe e só existe quando aquela equipe possui mais de um integrante. Assim, 1x1 e equipes individuais de partidas multiequipe não recebem MVP.

| Distinção | PvP humano | PvP híbrido |
| --- | ---: | ---: |
| MVP humano | +2 carreira | +1 carreira |
| MVP controlado pela IA | não se aplica | distintivo por dificuldade |
| Destaque da equipe derrotada | +1 carreira | +1 carreira |

O destaque da derrota precisa liderar ao menos um pilar, alcançar índice geral mínimo de 0,55 e ficar no máximo 0,15 atrás do líder. Um jogador que já recebeu MVP não acumula esse destaque. As estatísticas das IAs são agrupadas pela dificuldade escolhida, independentemente da civilização, e exibidas como média no ranking exclusivo de IAs.
