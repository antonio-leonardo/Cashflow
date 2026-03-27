# Configuração e Docker Compose

Os serviços locais seguem o `docker-compose.yml` na raiz do repositório.

## serviços no compose base

| serviço | Container | Portas | Credenciais / Uso |
|---|---|---|---|
| postgres | cashflow-postgres | 5432 | User: `postgres`, Password: `postgres`, DB: `cashflow` |
| mongo | cashflow-mongo | 27017 | Sem auth (dev) |
| redis | cashflow-redis | 6379 | Sem auth |
| rabbitmq | cashflow-rabbitmq | 5672, 15672 | `guest`/`guest`, UI em `http://localhost:15672` |
| keycloak | cashflow-keycloak | 8081 | Admin: `admin`/`admin` |
| jaeger | cashflow-jaeger | 16686, 4317, 4318 | Traces OpenTelemetry |
| gateway | cashflow-gateway | 5000 | Entrada de borda (YARP) |
| transaction-api | cashflow-transaction-api | 5001 | Write API |
| balance-query-api | cashflow-balance-query-api | 5002 | Read API (saldo diário) |
| worker-outbox | cashflow-worker-outbox-1 | - | Publicação de eventos |
| balance-worker | cashflow-balance-worker-1 | - | Consolidação em Redis |
| report-worker | cashflow-report-worker-1 | - | Read model de relatório |
| audit-worker | cashflow-audit-worker-1 | - | Read model de auditoria |

## Variáveis de ambiente mais usadas

- `ConnectionStrings__Postgres`
- `RabbitMq__Host`
- `RabbitMq__Port`
- `RabbitMq__Username`
- `RabbitMq__Password`
- `Redis__Connection`
- `Mongo__Connection`
- `Keycloak__Authority`
- `Keycloak__Audience`
- `OpenTelemetry__Otlp__Endpoint`

## Endereços úteis

- Gateway: `http://localhost:5000`
- Transaction API: `http://localhost:5001`
- Balance Query API: `http://localhost:5002`
- Keycloak: `http://localhost:8081`
- Jaeger: `http://localhost:16686`

## Overrides adicionais

- `docker-compose.ha.yml`: Execução local com foco em failover (réplicas de API).
- `docker-compose.tls.yml`: HTTPS local fim-a-fim (gateway -> APIs).
