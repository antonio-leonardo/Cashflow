# ADR-002: Integracao Event-Driven vs Sincrona

Date: 2026-03-20  
Status: Accepted

## Context
O sistema precisa desacoplar a criacao de transacoes da atualizacao de saldos, relatorios e auditoria, evitando chamadas sincrona em cascata.

## Decision
Adotar integracao por eventos com Outbox Pattern e mensageria assincrona entre servicos.

## Alternatives considered
- Chamadas HTTP sincrona entre servicos.
- Filas internas com polling sem broker.

## Consequences
Positivas:
- Desacoplamento real entre servicos.
- Maior tolerancia a falhas e backpressure.
- Escalabilidade por consumidor.

Negativas:
- Consistencia eventual.
- Necessidade de idempotencia e versionamento de eventos.