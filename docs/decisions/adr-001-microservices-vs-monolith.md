# ADR-001: Microservices vs Monolith

Date: 2026-03-20
Status: Accepted

## Context
O domínio de Transações exige Evolução independente de write e read models, além de isolamento de falhas entre processos de leitura e de escrita.

## Decision
Adotar arquitetura de Microsserviços com serviços separados para API de Transações e workers de leitura, integrados por mensageria.

## Alternatives considered
- Monolito modular com filas internas.
- Modular monolith com uma base de dados única.

## Consequences
Positivas:
- Isolamento de falhas e deploy independente.
- Escalabilidade horizontal por serviço.
- Evolução independente de read models.

Negativas:
- Maior complexidade operacional.
- Observabilidade e testes de Integração mais exigentes.

