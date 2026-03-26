# Catálogo SLI/SLO por serviço

## Objetivo

Transformar metas técnicas em catálogo operacional com medição recorrente.

## Catálogo

| serviço | SLI | SLO | Fonte de medição | Evidência atual |
|---|---|---|---|---|
| Gateway | Taxa de erro em borda | `http_req_failed <= 5%` em cenário NFR | k6 + traces/metrics OTel | `Back.End/Tests/Performance/k6/K6ThroughputE2ETests.cs` |
| Transaction API | Latência p95 de escrita | `p95 <= 1500ms` em 50 req/s | k6 + logs estruturados | `Back.End/Tests/Performance/results/transactions-throughput-summary.json` |
| Balance Query API | Latência p95 de consolidado diário | `p95 <= 1500ms` em 50 req/s | k6 modo `daily-balance` | `Back.End/Tests/Performance/results/daily-balance-throughput-summary.json` |
| Pipeline de eventos | Tempo de recuperação após falha de worker | Recuperação dentro da janela dos testes E2E/Holistic | E2E/Integration + Redis/Mongo verificação | `Back.End/Tests/IntegrationTests/Holistic/HolisticIntegrationTests.cs` |
| Integridade de read model | Sem duplicação por reentrega | Sem divergência em deduplicação/Idempotência | Integração + estado persistido | `IdempotencyReadinessHealthCheck` e suites E2E |

## Governança recomendada

1. Revisão semanal das metas e violações por serviço.
2. Publicação mensal do scorecard SLI/SLO no repositório.
3. Gate de release: bloquear deploy se NFR de borda não cumprir SLO.
4. Auditoria trimestral de thresholds para ajustar capacidade.
