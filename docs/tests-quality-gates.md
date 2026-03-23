# Testes e Qualidade (Acao 7)

Status: Active  
Ultima atualizacao: 2026-03-23

## Objetivo

Consolidar criterios de qualidade em gates executaveis, com foco em confiabilidade, integracao robusta e requisitos nao funcionais.

## Gates de qualidade

| Gate | Objetivo | Suite / Evidencia | Criterio de aprovacao |
|---|---|---|---|
| QG-01 | Compatibilidade de contrato na borda | `Back.End/Tests/ContractTests/Gateway` | Sem quebra de contrato entre Gateway e Transaction API |
| QG-02 | Integracao robusta por mensageria | `Back.End/Tests/IntegrationTests/Messaging/RabbitMqDecouplingIntegrationTests.cs` | Fan-out entre consumidores e roteamento para DLQ validos |
| QG-03 | Independencia entre servicos | `Back.End/Tests/E2E/*/ServiceIndependenceE2ETests.cs` | Write path continua disponivel com worker isolado em falha |
| QG-04 | NFR aprofundado sob carga | `Back.End/Tests/Performance/k6/K6ThroughputE2ETests.cs` | `http_req_failed <= 5%` e `p95 <= 1500ms` |
| QG-05 | Fluxo holistico autenticado | `Back.End/Tests/IntegrationTests/Holistic/HolisticIntegrationTests.cs` | Requisicao autenticada percorre pipeline e materializa read models |
| QG-06 | Seguranca de borda (authn/authz) | `Back.End/Tests/IntegrationTests/Holistic/HolisticIntegrationTests.cs` | `401` sem token/invalido, `403` sem escopo write e `201` com credencial valida |

## Execucao recomendada

Execucao completa:

```powershell
powershell -ExecutionPolicy Bypass -File Back.End/Tests/run-holistic-validation.ps1
```

Execucao rapida (triagem):

```powershell
powershell -ExecutionPolicy Bypass -File Back.End/Tests/run-holistic-validation.ps1 -Quick
```

Saida consolidada:

- `TestResults/holistic-validation-summary.json`
