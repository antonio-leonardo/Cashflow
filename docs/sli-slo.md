# Cashflow - Métricas Operacionais (SLI/SLO)

Este documento define de forma objetiva as Métricas operacionais esperadas para o desafio, mapeando cada objetivo não-funcional para evidências executáveis no repositório.

## Disponibilidade (Availability)

| SLI | Definição | SLO/Meta | Evidência/Teste |
|---|---|---|---|
| Write path sob falha do consolidado | Percentual de requisições HTTP falhas e latência p95 do endpoint de controle (`POST /api/transactions`) quando a materialização do consolidado diário está indisponível | `http_req_failed <= 0.05` e `http_req_duration p(95) <= 1500ms` | `Back.End/Tests/Performance/k6/K6ThroughputE2ETests.cs` (`TransactionApi_Should_Stay_Available_Under_Load_When_BalanceWorker_Is_Down`) e `Back.End/Tests/Performance/k6/transactions-throughput.js` (modo `transactions`) |
| Consolidado diário sob carga | Percentual de requisições HTTP falhas e latência p95 do endpoint do consolidado diário (`GET /api/balance/daily/{accountId}?date=yyyy-MM-dd`) sob pico | `http_req_failed <= 0.05` e `http_req_duration p(95) <= 1500ms` | `Back.End/Tests/Performance/k6/K6ThroughputE2ETests.cs` (`BalanceDailyApi_Should_Handle_50Rps_With_Max_5Percent_Loss`) e `Back.End/Tests/Performance/k6/transactions-throughput.js` (modo `daily-balance`) |

## Confiabilidade (Reliability)

| SLI | Definição | SLO/Meta | Evidência/Teste |
|---|---|---|---|
| Recuperação do pipeline após reinício | Tempo e evidência de catch-up do read model após restart/parada do processador | Read model materializa em janela observada nos testes de pipeline (eventual consistência) | `Back.End/Tests/E2E/*/BalancePipelineE2ETests.cs`, `ReportPipelineE2ETests.cs`, `AuditPipelineE2ETests.cs` e `Back.End/Tests/IntegrationTests/Holistic/HolisticIntegrationTests.cs` |

## Integridade (Integrity)

| SLI | Definição | SLO/Meta | Evidência/Teste |
|---|---|---|---|
| Idempotência no processamento | Reentregas do mesmo evento não devem produzir efeitos duplicados no read model | Consumidores são idempotentes (deduplicação) e pipeline recupera sem corromper consistência | `Back.End/Service/Transaction/Infrastructure/Persistence/*` (store `ProcessedAt`) + readiness `IdempotencyReadinessHealthCheck` e testes de pipeline/catch-up |

## Observabilidade (para sustentar as Métricas)

| SLI | Definição | Evidência/Teste |
|---|---|---|
| CorrelationId e rastreio distribuído | Propagação de `X-Correlation-Id` e traces entre gateway->apis->workers | Implementação de middleware e testes que validam metadados em `Back.End/Tests/IntegrationTests/Messaging/RabbitMqDecouplingIntegrationTests.cs` e uso de OpenTelemetry/Jaeger no runtime local |

## Governança

- Catálogo operacional por serviço: `docs/sli-slo-catalog.md`
