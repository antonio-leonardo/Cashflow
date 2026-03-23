# Matriz Holistica (Acao 8)

Status: Active  
Ultima atualizacao: 2026-03-23

## Objetivo

Consolidar os temas 1 a 8 em uma visao unica de execucao e evidencias tecnicas.

## Matriz 1-8

| Tema | Descricao | Status bool | Evidencia principal |
|---|---|---:|---|
| 1 | Estabilidade da suite (base) | `true` | `Back.End/Tests/Shared/DockerHostBootstrap.cs` e suites estaveis em Testcontainers |
| 2 | Independencia de servicos | `true` | `Back.End/Tests/E2E/*/ServiceIndependenceE2ETests.cs` |
| 3 | NFR inicial de carga | `true` | `Back.End/Tests/Performance/k6/transactions-throughput.js` |
| 4 | Decisoes arquiteturais explicitas | `true` | `docs/decisions/decision-matrix.md` + ADRs |
| 5 | NFR aprofundado | `true` | `Back.End/Tests/Performance/k6/K6ThroughputE2ETests.cs` |
| 6 | Integracao robusta entre componentes | `true` | `Back.End/Tests/IntegrationTests/Messaging/RabbitMqDecouplingIntegrationTests.cs` |
| 7 | Qualidade e gates consolidados | `true` | `docs/tests-quality-gates.md` + `Back.End/Tests/run-holistic-validation.ps1` |
| 8 | Execucao holistica integrada (1-7) | `true` | `Back.End/Tests/run-holistic-validation.ps1` + `TestResults/holistic-validation-summary.json` |

## Comando unico de validacao

```powershell
powershell -ExecutionPolicy Bypass -File Back.End/Tests/run-holistic-validation.ps1
```

## Artefato de saida

- `TestResults/holistic-validation-summary.json`
