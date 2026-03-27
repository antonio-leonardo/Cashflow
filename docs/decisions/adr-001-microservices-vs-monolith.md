# ADR-001: Microsserviços vs. Monolito

Date: 2026-03-20
Status: Accepted

## Context
O domínio de transações exige evolução independente entre write model e read models, além de isolamento de falhas entre os processos de leitura e escrita.

## Decision
Adotar arquitetura de microsserviços, com serviços separados para a API de transações e para os workers de leitura, integrados por mensageria.

## Alternatives considered
- Monolito modular com filas internas.
- Monolito modular com base de dados única.

## Consequences
Positivas:
- Isolamento de falhas e deploy independente.
- Escalabilidade horizontal por serviço.
- Evolução independente dos read models.

Negativas:
- Maior complexidade operacional.
- Observabilidade e testes de integração mais exigentes.

