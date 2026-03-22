# ADR-003: Database per Service + CQRS

Date: 2026-03-20  
Status: Accepted

## Context
Transacoes exigem consistencia forte no write model, enquanto consultas de saldo e relatorios exigem alta performance e modelos otimizados.

## Decision
Separar Write Model e Read Models (CQRS) e manter um banco por servico (database-per-service).

## Alternatives considered
- Banco unico compartilhado entre servicos.
- Read models dentro do mesmo banco transacional.

## Consequences
Positivas:
- Autonomia de cada contexto.
- Escalabilidade e performance no read side.
- Reducao de acoplamento entre equipes.

Negativas:
- Queries cross-service exigem materializacao por eventos.
- Maior custo operacional e de sincronizacao.