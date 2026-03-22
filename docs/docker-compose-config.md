# Configuracao e Docker Compose

Os connection strings e servicos seguem o `docker-compose.yml` na raiz do repositorio.

## Servicos no Docker Compose

| Servico | Container | Portas | Credenciais / Uso |
|---|---|---|---|
| postgres | cashflow-postgres | 5432 | User: `postgres`, Password: `postgres`, DB: `cashflow` |
| mongo | cashflow-mongo | 27017 | Sem auth (dev) |
| redis | cashflow-redis | 6379 | Sem auth |
| rabbitmq | cashflow-rabbitmq | 5672, 15672 | `guest`/`guest`, UI: `http://localhost:15672` |
| keycloak | cashflow-keycloak | 8081 (host) | Admin: `admin`/`admin` |
| gateway | cashflow-gateway | 5000 | Roteia para Transaction API |
| transaction-api | cashflow-transaction-api | 5001 | API de transacoes |

## Connection Strings (appsettings)

PostgreSQL (Transaction API + Outbox Worker):

- Host: `localhost` (se a API rodar no host)
- Host: `postgres` (se a API rodar no compose)

Exemplos:

- `Host=localhost;Port=5432;Database=cashflow;Username=postgres;Password=postgres`
- `Host=postgres;Port=5432;Database=cashflow;Username=postgres;Password=postgres`

## Variaveis de ambiente mais usadas

- `ConnectionStrings__Postgres`
- `RabbitMq__Host`
- `RabbitMq__Port`
- `RabbitMq__Username`
- `RabbitMq__Password`
- `Redis__Connection`
- `Mongo__Connection`
- `Keycloak__Authority` (ex: `http://localhost:8081/realms/cashflow`)
- `Keycloak__Audience` (ex: `cashflow-api`)

## Enderecos uteis

- Keycloak: `http://localhost:8081`
- Gateway: `http://localhost:5000`
- Transaction API: `http://localhost:5001`