# Matriz de Decisões Arquiteturais (ação 4)

Status: Ativo
Última atualização: 2026-03-22

## Objetivo

Consolidar, em um único ponto, as Decisões arquiteturais explícitas do projeto, com racional técnico, trade-offs e evidências de implementação/teste.

## Matriz consolidada

| ID | Decisão | Problema que resolve | Alternativas avaliadas | Trade-offs | Risco principal | Mitigação adotada | Evidência |
|---|---|---|---|---|---|---|---|
| ADR-001 | Microsserviços para write/read | Isolamento de falhas e Evolução independente por contexto | Monolito modular; monolito com banco único | + Isolamento/escala por serviço; - operação mais complexa | Aumento de custo operacional | Observabilidade, healthchecks, suites E2E por contexto | `docs/decisions/adr-001-microservices-vs-monolith.md`, `Back.End/Tests/E2E` |
| ADR-002 | Integração assíncrona por eventos + Outbox | Desacoplamento entre transação e consolidações | HTTP sync em cascata; fila interna sem broker | + Backpressure e resiliência; - consistência eventual | Entrega duplicada ou fora de ordem | Idempotência, envelope de metadados, retries e DLQ | `docs/decisions/adr-002-event-driven-vs-sync.md`, `Back.End/Outbox/Worker/Worker.cs`, `Back.End/Tests/IntegrationTests/Messaging/RabbitMqDecouplingIntegrationTests.cs` |
| ADR-003 | CQRS + Database per Service | Otimização distinta para escrita forte e leitura performática | Banco único compartilhado | + Autonomia/performance no read side; - Materialização cross-service | Divergência temporária entre write/read | Pipeline de eventos e testes E2E de projeções | `docs/decisions/adr-003-db-per-service-cqrs.md`, `Back.End/Worker/*` |
| ADR-004 | Gateway + Keycloak (OIDC/OAuth2) | Segurança centralizada na borda | Auth em cada serviço; gateway proprietário | + política única; - Dependência do IdP | Regressão de auth por mudança de contrato | Testes de Integração Gateway+Keycloak | `docs/decisions/adr-004-gateway-auth-keycloak.md`, `Back.End/Tests/IntegrationTests/Holistic` |

## Critérios arquiteturais de aceitação (ação 4)

- Toda decisão crítica precisa de ADR rastreável.
- Toda decisão crítica precisa mostrar pelo menos uma alternativa descartada e o motivo.
- Toda decisão crítica precisa apontar risco + mitigação.
- Toda decisão crítica precisa apontar evidência técnica (código, teste ou diagrama).

## Rastreabilidade rápida

- Visão executiva: `README.md` (tópico 2).
- Detalhamento: arquivos ADR em `docs/decisions/`.
- Arquitetura e fluxos: `docs/architecture.md`.
