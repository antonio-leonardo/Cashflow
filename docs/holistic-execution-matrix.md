# Matriz holística (ação 8)

Status: Ativo
Última atualização: 2026-03-23

## Objetivo

Consolidar os temas 1 a 8 em uma visão única de execução e evidências técnicas.

## Matriz 1-8

| Tema | Descrição | Status bool | Evidência principal |
|---|---|---:|---|
| 1 | Estabilidade da suite (base) | `true` | `Back.End/Tests/Shared/DockerHostBootstrap.cs` e suites estáveis em Testcontainers |
| 2 | Independência de serviços | `true` | `Back.End/Tests/E2E/*/ServiceIndependenceE2ETests.cs` |
| 3 | NFR inicial de carga | `true` | `Back.End/Tests/Performance/k6/transactions-throughput.js` |
| 4 | Decisões arquiteturais explícitas | `true` | `docs/decisions/decision-matrix.md` + ADRs |
| 5 | NFR aprofundado | `true` | `Back.End/Tests/Performance/k6/K6ThroughputE2ETests.cs` |
| 6 | Integração robusta entre componentes | `true` | `Back.End/Tests/IntegrationTests/Messaging/RabbitMqDecouplingIntegrationTests.cs` |
| 7 | Qualidade e gates consolidados | `true` | `docs/tests-quality-gates.md` + `Back.End/Tests/run-holistic-validation.ps1` |
| 8 | Execução holística integrada (1-7) | `true` | `Back.End/Tests/run-holistic-validation.ps1` + `TestResults/holistic-validation-summary.json` |

## Comando único de validação

```powershell
powershell -ExecutionPolicy Bypass -File Back.End/Tests/run-holistic-validation.ps1
```

## Artefato de saída

- `TestResults/holistic-validation-summary.json`
