# Estatísticas pós-jogo e replays

O Age Nexus aceita relatórios pós-jogo por replay ou por preenchimento manual. O replay é uma fonte de dados, não uma garantia de que todas as estatísticas finais estejam presentes: versões diferentes do Age II gravam conjuntos diferentes de informações.

## Formatos e dependência

O importador aceita `.aoe2record`, `.mgz` e `.mgx`, com limite de 50 MB. A aplicação chama um processo Python isolado que usa `aoc-mgz` (`mgz`) para interpretar o arquivo.

Instale a dependência no mesmo ambiente em que a aplicação será executada:

```bash
python3 -m pip install -r src/AgeNexus.Infrastructure/ReplayAnalysis/requirements.txt
```

O executável padrão é `python3`. Em ambientes nos quais o comando é outro, configure:

```json
{
  "ReplayExtractor": {
    "PythonExecutable": "python"
  }
}
```

`ReplayExtractor:ScriptPath` pode apontar para uma cópia externa de `extract_replay.py`. Sem essa configuração, o script copiado para a saída da aplicação é usado.

## Fluxo

1. O servidor valida extensão e tamanho e calcula o SHA-256 do replay.
2. O extrator devolve somente valores que encontrou diretamente no arquivo.
3. Campos ausentes ficam nulos; o sistema não inventa valores.
4. Um participante completa manualmente os dados ausentes usando as telas finais ou capturas.
5. O relatório completo é enviado e exige uma decisão por equipe humana.
6. Depois que a partida e o relatório estão validados, a fórmula versionada calcula o desempenho e registra os bônus no livro de pontos.

O arquivo bruto não é persistido por esta fatia: apenas nome, SHA-256, versão do extrator, cobertura e estatísticas normalizadas. Se o armazenamento definitivo do replay for desejado, ele deve ser integrado ao armazenamento de objetos já previsto para evidências.

## Integridade e limitações

- O SHA-256 impede reutilização silenciosa do mesmo replay em mais de uma partida.
- Um replay pode exigir preenchimento manual, sobretudo para `maior exército` e versões sem o bloco final de conquistas.
- Dados manuais e transcritos de capturas seguem o mesmo fluxo de confirmação das equipes.
- Relatórios contestados não geram bônus.
- O bônus nunca altera rating competitivo.
- Em PvE, o resultado de desempenho gera somente distintivo, sem pontos de carreira.

O parser deve ser exercitado com arquivos reais das versões usadas pelo grupo sempre que `mgz` ou o Age II forem atualizados. A versão do extrator fica gravada em cada relatório para permitir auditoria e recálculo.

## Fórmula de desempenho

As quatro pontuações finais oficiais são normalizadas entre os humanos da mesma partida. Em 1x1, os pesos são militar 45%, economia 35%, tecnologia 10% e sociedade 10%. Em jogos com equipes humanas, são 40%, 30%, 10% e 20%, respectivamente.

| Distinção | PvP humano | PvP híbrido | PvE puro |
| --- | ---: | ---: | ---: |
| MVP único | +2 carreira | +1 carreira | distintivo |
| MVP empatado (diferença até 0,02) | +1 por jogador | +1 por jogador | distintivo |
| Destaque da equipe derrotada | +1 carreira | +1 carreira | não se aplica |

O destaque da derrota precisa liderar ao menos um pilar, alcançar índice geral mínimo de 0,55 e ficar no máximo 0,15 atrás do líder. Um jogador que já recebeu MVP não acumula esse destaque. Partidas PvE com apenas um humano não concedem MVP automático.

