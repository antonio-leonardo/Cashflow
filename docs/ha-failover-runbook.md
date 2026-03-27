# HA Failover Runbook (Local Evidence)

## Objetivo

Gerar evidência reproduzível de continuidade operacional com réplicas de API.

## Pre-requisitos

- Docker Desktop ativo
- Stack base funcional

## Execução manual

```bash
docker compose -f docker-compose.yml -f docker-compose.ha.yml up -d \
  --scale transaction-api=2 \
  --scale balance-query-api=2 \
  gateway transaction-api balance-query-api postgres rabbitmq redis keycloak
```

Em seguida, pare uma réplica de `transaction-api` e valide disponibilidade do gateway:

```bash
docker stop cashflow-transaction-api-1
curl -i http://localhost:5000/health/ready
```

## Execução automatizada

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run-ha-failover-demo.ps1
```

O script:

1. Sobe a stack com `transaction-api=2` e `balance-query-api=2`.
2. Para uma réplica de `transaction-api`.
3. Consulta repetidamente `GET /health/ready` no gateway.
4. Reinicia a réplica parada e encerra o ambiente (salvo `-KeepStack`).

## Evidências esperadas

- Logs mostrando parada de réplica sem indisponibilidade total do gateway.
- Status `200` recorrente em `/health/ready` durante o teste.
- Registro do comando de escala e do comando de stop/start.
