# Cashflow - Metricas Operacionais (SLI/SLO)

Este documento define de forma objetiva as metricas operacionais esperadas para o desafio, mapeando cada objetivo nao-funcional para evidencias executaveis no repositorio.

## Disponibilidade (Availability)

| SLI | Definicao | SLO/Meta | Evidencia/Teste |
|---|---|---|---|
| Write path sob falha do consolidado | Percentual de requisicoes HTTP falhas e latencia p95 do endpoint de controle (`POST /api/transactions`) quando a materializacao do consolidado diario esta indisponivel | `http_req_failed <= 0.05` e `http_req_duration p(95) <= 1500ms` | `Back.End/Tests/Performance/k6/K6ThroughputE2ETests.cs` (`TransactionApi_Should_Stay_Available_Under_Load_When_BalanceWorker_Is_Down`) e `Back.End/Tests/Performance/k6/transactions-throughput.js` (modo `transactions`) |
| Consolidado diario sob carga | Percentual de requisicoes HTTP falhas e latencia p95 do endpoint do consolidado diario (`GET /api/balance/daily/{accountId}?date=yyyy-MM-dd`) sob pico | `http_req_failed <= 0.05` e `http_req_duration p(95) <= 1500ms` | `Back.End/Tests/Performance/k6/K6ThroughputE2ETests.cs` (`BalanceDailyApi_Should_Handle_50Rps_With_Max_5Percent_Loss`) e `Back.End/Tests/Performance/k6/transactions-throughput.js` (modo `daily-balance`) |

## Confiabilidade (Reliability)

| SLI | Definicao | SLO/Meta | Evidencia/Teste |
|---|---|---|---|
| Recuperacao do pipeline apos reinicio | Tempo e evidencia de catch-up do read model apos restart/parada do processador | Read model materializa em janela observada nos testes de pipeline (eventual consistencia) | `Back.End/Tests/E2E/*/BalancePipelineE2ETests.cs`, `ReportPipelineE2ETests.cs`, `AuditPipelineE2ETests.cs` e `Back.End/Tests/IntegrationTests/Holistic/HolisticIntegrationTests.cs` |

## Integridade (Integrity)

| SLI | Definicao | SLO/Meta | Evidencia/Teste |
|---|---|---|---|
| Idempotencia no processamento | Reentregas do mesmo evento nao devem produzir efeitos duplicados no read model | Consumidores sao idempotentes (deduplicacao) e pipeline recupera sem corromper consistencia | `Back.End/Service/Transaction/Infrastructure/Persistence/*` (store `ProcessedAt`) + readiness `IdempotencyReadinessHealthCheck` e testes de pipeline/catch-up |

## Observabilidade (para sustentar as metricas)

| SLI | Definicao | Evidencia/Teste |
|---|---|---|
| CorrelationId e rastreio distribuido | Propagacao de `X-Correlation-Id` e traces entre gateway->apis->workers | Implementacao de middleware e testes que validam metadados em `Back.End/Tests/IntegrationTests/Messaging/RabbitMqDecouplingIntegrationTests.cs` e uso de OpenTelemetry/Jaeger no runtime local |

