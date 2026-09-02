# ADR 0002: modelar partidas por equipes e participantes

- Status: aceito
- Data: 2026-08-30

## Decisão

`Match` é a raiz do agregado e contém uma coleção arbitrária de equipes. Cada equipe contém participantes tipados como humano ou IA. Formatos como `2x2` não são enums e são derivados das quantidades de humanos. O resultado fica na equipe.

## Consequências

O mesmo modelo atende equipes iguais, assimétricas e partidas contra IA sem mudanças de esquema. As invariantes de composição ficam no agregado.

