# Matriz de Decisoes Arquiteturais (Acao 4)

Status: Active  
Ultima atualizacao: 2026-03-22

## Objetivo

Consolidar, em um unico ponto, as decisoes arquiteturais explicitas do projeto, com racional tecnico, trade-offs e evidencias de implementacao/teste.

## Matriz consolidada

| ID | Decisao | Problema que resolve | Alternativas avaliadas | Trade-offs | Risco principal | Mitigacao adotada | Evidencia |
|---|---|---|---|---|---|---|---|
| ADR-001 | Microsservicos para write/read | Isolamento de falhas e evolucao independente por contexto | Monolito modular; monolito com banco unico | + Isolamento/escala por servico; - Operacao mais complexa | Aumento de custo operacional | Observabilidade, healthchecks, suites E2E por contexto | `docs/decisions/adr-001-microservices-vs-monolith.md`, `Back.End/Tests/E2E` |
| ADR-002 | Integracao assincrona por eventos + Outbox | Desacoplamento entre transacao e consolidacoes | HTTP sync em cascata; fila interna sem broker | + Backpressure e resiliencia; - Consistencia eventual | Entrega duplicada ou fora de ordem | Idempotencia, envelope de metadados, retries e DLQ | `docs/decisions/adr-002-event-driven-vs-sync.md`, `Back.End/Outbox/Worker/Worker.cs`, `Back.End/Tests/IntegrationTests/Messaging/RabbitMqDecouplingIntegrationTests.cs` |
| ADR-003 | CQRS + Database per Service | Otimizacao distinta para escrita forte e leitura performatica | Banco unico compartilhado | + Autonomia/performance no read side; - Materializacao cross-service | Divergencia temporaria entre write/read | Pipeline de eventos e testes E2E de projecoes | `docs/decisions/adr-003-db-per-service-cqrs.md`, `Back.End/Worker/*` |
| ADR-004 | Gateway + Keycloak (OIDC/OAuth2) | Seguranca centralizada na borda | Auth em cada servico; gateway proprietario | + Politica unica; - Dependencia do IdP | Regressao de auth por mudanca de contrato | Testes de integracao Gateway+Keycloak | `docs/decisions/adr-004-gateway-auth-keycloak.md`, `Back.End/Tests/IntegrationTests/Holistic` |

## Criterios arquiteturais de aceitacao (Acao 4)

- Toda decisao critica precisa de ADR rastreavel.
- Toda decisao critica precisa mostrar pelo menos uma alternativa descartada e o motivo.
- Toda decisao critica precisa apontar risco + mitigacao.
- Toda decisao critica precisa apontar evidencia tecnica (codigo, teste ou diagrama).

## Rastreabilidade rapida

- Visao executiva: `README.md` (Topico 2).
- Detalhamento: arquivos ADR em `docs/decisions/`.
- Arquitetura e fluxos: `docs/architecture.md`.
