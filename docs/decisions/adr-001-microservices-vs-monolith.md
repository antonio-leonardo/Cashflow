# ADR-001: Microservices vs Monolith

Date: 2026-03-20  
Status: Accepted

## Context
O dominio de transacoes exige evolucao independente de write e read models, alem de isolamento de falhas entre processos de leitura e de escrita.

## Decision
Adotar arquitetura de microsservicos com servicos separados para API de transacoes e workers de leitura, integrados por mensageria.

## Alternatives considered
- Monolito modular com filas internas.
- Modular monolith com uma base de dados unica.

## Consequences
Positivas:
- Isolamento de falhas e deploy independente.
- Escalabilidade horizontal por servico.
- Evolucao independente de read models.

Negativas:
- Maior complexidade operacional.
- Observabilidade e testes de integracao mais exigentes.