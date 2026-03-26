# API Versioning Strategy

## Objetivo

Padronizar evolução de contrato HTTP sem breaking change imediato para consumidores existentes.

## Convenção adotada

- Rotas canônicas versionadas por URL:
  - `POST /api/v1/transactions`
  - `GET /api/v1/transactions/{id}`
  - `GET /api/v1/balance/daily/{accountId}?date=yyyy-MM-dd`
- Rotas legadas (`/api/...`) permanecem ativas temporariamente para transição.

## Governança de evolução

1. Toda mudança incompatível cria nova versão (`/api/v2/...`).
2. `v1` permanece estável até janela de depreciação acordada.
3. Contratos versionados entram em testes de integração e contrato.
4. Gateway e APIs devem expor versão canônica em documentação/monitoramento.

## Evidências no repositório

- Rotas `v1` implementadas em:
  - `Back.End/Service/Transaction/API/Program.cs`
  - `Back.End/Service/Balance/API/Program.cs`
  - `Back.End/Gateway/appsettings.json`
- Testes usando `v1`:
  - `Back.End/Tests/IntegrationTests/Holistic/HolisticIntegrationTests.cs`
  - `Back.End/Tests/Performance/k6/transactions-throughput.js`
  - `Back.End/Tests/ContractTests/Gateway/GatewayTransactionContractTests.cs`
  - `Back.End/Tests/ContractTests/Balance/GatewayBalanceQueryContractTests.cs`
