# ADR-002: Integração Event-Driven vs Síncrona

Date: 2026-03-20
Status: Accepted

## Context
O sistema precisa desacoplar a criação de transações da atualização de saldos, relatórios e auditoria, evitando chamadas síncronas em cascata.

## Decision
Adotar integração por eventos com Outbox Pattern e mensageria assíncrona entre serviços.

## Alternatives considered
- Chamadas HTTP síncronas entre serviços.
- Filas internas com polling sem broker.

## Consequences
Positivas:
- Desacoplamento real entre serviços.
- Maior tolerância a falhas e backpressure.
- Escalabilidade por consumidor.

Negativas:
- Consistência eventual.
- Necessidade de idempotência e versionamento de eventos.
