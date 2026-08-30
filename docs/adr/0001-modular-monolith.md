# ADR 0001: iniciar com monólito modular

- Status: aceito
- Data: 2026-08-30

## Decisão

Construir uma aplicação implantável, organizada por módulos funcionais e camadas Domain, Application, Infrastructure e Web. Regras de negócio não dependem da interface nem da persistência.

## Consequências

A implantação e as transações permanecem simples no MVP. Limites explícitos preservam uma separação futura sem assumir agora o custo de microsserviços.
