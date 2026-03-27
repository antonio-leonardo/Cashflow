# Testes e Qualidade (ação 7)

Status: Ativo
Última atualização: 2026-03-23

## Objetivo

Consolidar critérios de qualidade em gates executáveis, com foco em confiabilidade, integração robusta e requisitos não funcionais.

## Gates de qualidade

| Gate | Objetivo | Suíte / Evidência | Critério de aprovação |
|---|---|---|---|
| QG-01 | Compatibilidade de contrato na borda (write) | `Back.End/Tests/ContractTests/Gateway` | Sem quebra de contrato entre Gateway e Transaction API |
| QG-01B | Compatibilidade de contrato na borda (read) | `Back.End/Tests/ContractTests/Balance` | Sem quebra de contrato entre Gateway e Balance Query API |
| QG-02 | Integração robusta por mensageria | `Back.End/Tests/IntegrationTests/Messaging/RabbitMqDecouplingIntegrationTests.cs` | Fan-out entre consumidores e roteamento para DLQ válidos |
| QG-03 | Independência entre serviços | `Back.End/Tests/E2E/*/ServiceIndependenceE2ETests.cs` | Write path permanece disponível com worker isolado em falha |
| QG-04 | NFR aprofundado sob carga | `Back.End/Tests/Performance/k6/K6ThroughputE2ETests.cs` | `http_req_failed <= 5%` e `p95 <= 1500ms` |
| QG-05 | Fluxo holístico autenticado | `Back.End/Tests/IntegrationTests/Holistic/HolisticIntegrationTests.cs` | Requisição autenticada percorre pipeline e materializa read models |
| QG-06 | Segurança de borda (authn/authz) | `Back.End/Tests/IntegrationTests/Holistic/HolisticIntegrationTests.cs` | `401` sem token/inválido, `403` sem escopo write e `201` com credencial válida |
| QG-07 | Recuperação após falha de processador | `Back.End/Tests/IntegrationTests/Holistic/HolisticIntegrationTests.cs` | Write path permanece disponível, e o pipeline de eventos se recupera após reinício do Outbox Worker |
| QG-08 | Saúde operacional de serviços | `Back.End/Tests/IntegrationTests/Holistic/HolisticIntegrationTests.cs` | Endpoints `/health/live` e `/health/ready` respondem com `200 OK` |
| QG-09 | Versionamento e compatibilidade de consulta | `Back.End/Tests/IntegrationTests/Balance/BalanceApiIntegrationTests.cs` | Rotas `/api/v1/...` ativas e compatíveis com rota legado `/api/...` |
| QG-10 | Idempotência no read side (balance) | `Back.End/Tests/E2E/Balance/BalancePipelineE2ETests.cs` (`Duplicate_EventId_Redelivery_Should_Be_Applied_Only_Once`) | Reentrega do mesmo `EventId` não duplica saldo total/diário |
| QG-11 | Resiliência configurável (policy-level) | `Back.End/Tests/DomainTests/Balance/ResiliencePoliciesTests.cs` | Fallback HTTP configurável e sanitização de opções inválidas validados |

## Execução recomendada

Execução completa:

```powershell
powershell -ExecutionPolicy Bypass -File Back.End/Tests/run-holistic-validation.ps1
```

Execução rápida (triagem):

```powershell
powershell -ExecutionPolicy Bypass -File Back.End/Tests/run-holistic-validation.ps1 -Quick
```

Saída consolidada:

- `TestResults/holistic-validation-summary.json`
