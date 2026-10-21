# ADR 0002: modelar partidas por equipes e participantes

- Status: aceito
- Data: 2026-08-30

## Decisão

`Match` é a raiz do agregado e contém de duas a oito equipes. Cada equipe contém participantes tipados como humano ou IA. Formatos como `2x2` e `1x1x1` não são enums e são derivados das quantidades de humanos. O resultado fica na equipe. Partidas de três a oito equipes usam a categoria todos contra todos; elas armazenam resultados e estatísticas, mas ainda não alteram o Elo, cujo cálculo atual compara exatamente dois lados.

## Consequências

O mesmo modelo atende equipes iguais, assimétricas, partidas contra IA e todos contra todos com até oito equipes sem mudanças de esquema. As invariantes de composição ficam no agregado.

