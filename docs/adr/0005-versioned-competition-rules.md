# ADR 0005 — Regras competitivas versionadas

## Status

Aceito em 2026-09-01.

## Decisão

As fórmulas de rating, carreira, PvE, partidas híbridas e prestígio de clãs pertencem ao domínio e são identificadas pela versão `2026.09`. Cada evento imutável de rating ou pontos grava a versão e os detalhes utilizados. PvP, PvE e carreira permanecem livros separados.

Partidas puramente PvE só entram no ranking oficial com evidência verificada ou auditada. Evidência básica concede 40% dos pontos, tem teto sazonal de 150 e não dá acesso ao ranking oficial. Um desafio de servidor, válido por 30 minutos e de uso único, vincula a evidência à configuração declarada. Replays usam hash SHA-256.

Partidas híbridas são PvP com atribuição reduzida: o delta recebe fator entre 0,50 e 0,85 conforme a proporção de IAs, e a carreira usa 70% da base ajustada pelo desafio. Elas não alimentam o ranking PvE.

## Consequências

- Mudanças de fórmula criam uma nova versão; não reescrevem silenciosamente o histórico.
- Correções e anulações geram eventos reversores idempotentes.
- Rankings e projeções podem ser reconstruídos a partir dos livros.
- O código de interface não calcula pontuação nem decide validade de evidência.
