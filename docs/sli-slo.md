# Cashflow - Métricas operacionais (SLI/SLO)

Este documento define, de forma objetiva, as métricas operacionais esperadas para o desafio, mapeando cada objetivo não funcional para evidências executáveis no repositório.

## Disponibilidade

| SLI | Definição | SLO/Meta | Evidência/Teste |
|---|---|---|---|
| Write path sob falha do consolidado | Percentual de requisições HTTP com falha e latência p95 do endpoint de controle (`POST /api/transactions`) quando a materialização do consolidado diário está indisponível | `http_req_failed <= 0.05` e `http_req_duration p(95) <= 1500ms` | `Back.End/Tests/Performance/k6/K6ThroughputE2ETests.cs` (`TransactionApi_Should_Stay_Available_Under_Load_When_BalanceWorker_Is_Down`) e `Back.End/Tests/Performance/k6/transactions-throughput.js` (modo `transactions`) |
| Consolidado diário sob carga | Percentual de requisições HTTP falhas e latência p95 do endpoint do consolidado diário (`GET /api/balance/daily/{accountId}?date=yyyy-MM-dd`) sob pico | `http_req_failed <= 0.05` e `http_req_duration p(95) <= 1500ms` | `Back.End/Tests/Performance/k6/K6ThroughputE2ETests.cs` (`BalanceDailyApi_Should_Handle_50Rps_With_Max_5Percent_Loss`) e `Back.End/Tests/Performance/k6/transactions-throughput.js` (modo `daily-balance`) |

## Confiabilidade

| SLI | Definição | SLO/Meta | Evidência/Teste |
|---|---|---|---|
| Recuperação do pipeline após reinício | Tempo e evidência de recuperação do read model após reinício/parada do processador | Read model materializado na janela observada nos testes de pipeline (consistência eventual) | `Back.End/Tests/E2E/*/BalancePipelineE2ETests.cs`, `ReportPipelineE2ETests.cs`, `AuditPipelineE2ETests.cs` e `Back.End/Tests/IntegrationTests/Holistic/HolisticIntegrationTests.cs` |

## Integridade

| SLI | Definição | SLO/Meta | Evidência/Teste |
|---|---|---|---|
| Idempotência no processamento | Reentregas do mesmo evento não devem produzir efeitos duplicados no read model | Consumidores idempotentes (deduplicação) e recuperação do pipeline sem corrupção de consistência | `Back.End/Worker/Balance/RedisBalanceRepository.cs` (deduplicação por chave de idempotência e atualização de saldo no mesmo script Lua atômico), `Back.End/Tests/E2E/Balance/BalancePipelineE2ETests.cs` (`Duplicate_EventId_Redelivery_Should_Be_Applied_Only_Once`), `Back.End/Worker/Audit/AuditDocument.cs` (chave única por evento), readiness `IdempotencyReadinessHealthCheck` e testes de recuperação do pipeline |

## Observabilidade (para sustentar as métricas)

| SLI | Definição | Evidência/Teste |
|---|---|---|
| CorrelationId e rastreio distribuído | Propagação de `X-Correlation-Id` e traces entre gateway -> APIs -> workers | Implementação de middleware e testes que validam metadados em `Back.End/Tests/IntegrationTests/Messaging/RabbitMqDecouplingIntegrationTests.cs`, além do uso de OpenTelemetry/Jaeger na execução local |

## Governança

- Catálogo operacional por serviço: `docs/sli-slo-catalog.md`
