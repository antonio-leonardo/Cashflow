# Configuração e Docker Compose

Os connection strings e serviços da aplicação seguem o que está definido em `docker-compose.yml` na raiz do repositório.

## Serviços no Docker Compose

| Serviço   | Container         | Portas        | Credenciais / Uso |
|-----------|-------------------|---------------|-------------------|
| **postgres** | cashflow-postgres | 5432          | User: `admin`, Password: `admin`, DB: `cashflow` |
| **mongodb**  | cashflow-mongo   | 27017         | Sem auth (dev)    |
| **redis**    | cashflow-redis   | 6379          | Sem auth          |
| **rabbitmq** | cashflow-rabbitmq| 5672, 15672   | Padrão: `guest`/`guest`, Management: http://localhost:15672 |
| **keycloak** | cashflow-keycloak| 8080          | Admin: `admin`/`admin` |

## Connection Strings (appsettings)

- **TransactionDb** (PostgreSQL – usado por Transaction.API e Outbox.Worker):
  - Em execução **no host** (com `docker compose up` apenas para os serviços):  
    `Host=localhost;Port=5432;Database=cashflow;Username=admin;Password=admin`
  - Em execução **dentro da rede Docker** (API/Workers como serviços do compose):  
    `Host=postgres;Port=5432;Database=cashflow;Username=admin;Password=admin`

Override via variável de ambiente:

- `ConnectionStrings__TransactionDb=Host=postgres;Port=5432;Database=cashflow;Username=admin;Password=admin`

## Demais serviços (futuros workers / mensageria)

- **MongoDB**: `mongodb://localhost:27017` (na rede Docker: `mongodb://mongodb:27017`)
- **Redis**: `localhost:6379` (na rede Docker: `redis:6379`)
- **RabbitMQ**: `amqp://guest:guest@localhost:5672/` (na rede Docker: `amqp://guest:guest@rabbitmq:5672/`)
- **Keycloak**: `http://localhost:8080/` (na rede Docker: `http://keycloak:8080/`)

Os valores acima devem ser usados nos appsettings (ou env) dos projetos que consumirem esses serviços, em alinhamento com o `docker-compose.yml`.
