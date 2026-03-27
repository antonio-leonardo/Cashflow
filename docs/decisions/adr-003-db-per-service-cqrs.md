# ADR-003: Database per Service + CQRS

Date: 2026-03-20
Status: Accepted

## Context
Transações exigem consistência forte no write model, enquanto as consultas de saldo e relatório exigem alta performance e modelos otimizados.

## Decision
Separar Write Model e Read Models (CQRS) e manter um banco por serviço (database-per-service).

## Alternatives considered
- Banco único compartilhado entre serviços.
- Read models dentro do mesmo banco transacional.

## Consequences
Positivas:
- Autonomia de cada contexto.
- Escalabilidade e performance no read side.
- Redução de acoplamento entre equipes.

Negativas:
- Consultas cross-service exigem materialização por eventos.
- Maior custo operacional e de sincronização.
