# Cashflow - Sistema de Transacoes Event-Driven

**Autor:** Antonio Leonardo  
**Plataforma:** .NET 10  
**Estilo arquitetural:** Microsservicos orientados a eventos  
**Estrategia:** Multicloud portavel (AWS, Azure, GCP)  
**Execucao local (TDD):** Visual Studio 2026 Community + Docker

---

## Indice

1. [Visao geral](#1-visao-geral)
2. [Decisoes arquiteturais e trade-offs](#2-decisoes-arquiteturais-e-trade-offs)
3. [Requisitos nao funcionais](#3-requisitos-nao-funcionais)
4. [Integracao entre componentes](#4-integracao-entre-componentes)
5. [Stack tecnologica](#5-stack-tecnologica)
6. [Arquitetura e diagramas](#6-arquitetura-e-diagramas)
7. [Fluxo principal](#7-fluxo-principal)
8. [Versionamento de eventos](#8-versionamento-de-eventos)
9. [Testes e qualidade](#9-testes-e-qualidade)
10. [Execucao local](#10-execucao-local)
11. [Estrutura da solution](#11-estrutura-da-solution)
12. [CI/CD](#12-cicd)
13. [SonarQube (Code Smells)](#13-sonarqube-code-smells)
14. [Roadmap](#14-roadmap)

---

## 1. Visao geral

O **Cashflow** e um sistema de transacoes financeiras construido sobre **Event-Driven Architecture**, **CQRS** e **Clean Architecture**. O objetivo central e garantir resiliencia, escalabilidade e portabilidade real entre clouds, sem lock-in tecnologico.

Principios fundamentais:

- Event-Driven: eventos imutaveis como contratos entre servicos
- CQRS: Write Model isolado dos Read Models por servico
- Clean Architecture: dominio independente de infraestrutura
- Outbox Pattern: consistencia atomica entre banco e mensageria
- Idempotencia: consumidores seguros a reentregas
- Observabilidade: CorrelationId + logs estruturados

```mermaid
flowchart LR
  client[Client]
  gw[API Gateway]
  txapi[Transaction API]
  writedb[(Write DB)]
  outbox[(OutboxEvents)]
  outboxw[Outbox Worker]
  broker[[Message Broker]]

  client --> gw --> txapi --> writedb --> outbox --> outboxw --> broker

  subgraph Workers
    bal[Balance Worker]
    rep[Report Worker]
    aud[Audit Worker]
  end

  broker --> bal
  broker --> rep
  broker --> aud

  bal --> baldb[(Balance Read DB)]
  rep --> repdb[(Report Read DB)]
  aud --> auddb[(Audit Read DB)]
```

---

## 2. Decisoes arquiteturais e trade-offs

As decisoes principais estao registradas em ADRs e, para facilitar avaliacao tecnica, estao consolidadas abaixo no README principal.
Matriz consolidada de decisao/riscos/mitigacoes: `docs/decisions/decision-matrix.md`.

### 2.1 ADR-001 - Microservices vs Monolith

Referencia: `docs/decisions/adr-001-microservices-vs-monolith.md`

Contexto:
- O dominio exige evolucao independente entre escrita (transacao) e leitura (saldo, relatorio e auditoria).
- Isolamento de falhas foi tratado como requisito de arquitetura.

Decisao:
- Adotar microsservicos: `transaction-api`, `outbox-worker`, `balance-worker`, `report-worker`, `audit-worker`.

Alternativas consideradas:
- Monolito modular com filas internas.
- Monolito modular com banco unico.

Trade-offs:
- Positivos: isolamento de falhas, deploy independente, escala horizontal por servico.
- Negativos: maior complexidade operacional e maior exigencia de observabilidade.

### 2.2 ADR-002 - Integracao Event-Driven vs Sincrona

Referencia: `docs/decisions/adr-002-event-driven-vs-sync.md`

Contexto:
- Evitar chamada sincronas em cascata no fluxo de consolidacao.
- Garantir desacoplamento real entre servicos de leitura e escrita.

Decisao:
- Integracao assincrona por eventos com `Outbox Pattern` + broker.

Alternativas consideradas:
- HTTP sincronico entre servicos.
- Filas internas sem broker e sem envelope de contrato.

Trade-offs:
- Positivos: desacoplamento, backpressure, maior tolerancia a falhas.
- Negativos: consistencia eventual, necessidade de idempotencia e versionamento de eventos.

### 2.3 ADR-003 - Database per Service + CQRS

Referencia: `docs/decisions/adr-003-db-per-service-cqrs.md`

Contexto:
- Escrita exige consistencia transacional.
- Leitura exige modelos especializados de alta performance.

Decisao:
- Separar write/read (CQRS) e manter banco por servico.

Alternativas consideradas:
- Banco unico compartilhado.
- Read models dentro do mesmo banco transacional.

Trade-offs:
- Positivos: autonomia de contexto, escalabilidade do read side e menor acoplamento.
- Negativos: consultas cross-service exigem materializacao por eventos.

### 2.4 ADR-004 - Gateway com Keycloak (OIDC)

Referencia: `docs/decisions/adr-004-gateway-auth-keycloak.md`

Contexto:
- Autenticacao deve ser centralizada e desacoplada do dominio.
- Necessidade de politica de acesso unificada na borda.

Decisao:
- Gateway (YARP) com autenticacao OIDC/OAuth2 via Keycloak.

Alternativas consideradas:
- Autenticacao distribuida em cada servico.
- Gateway com provedor proprietario.

Trade-offs:
- Positivos: governanca de acesso, isolamento do dominio e padrao unico de autenticacao.
- Negativos: dependencia adicional e necessidade de testes de integracao de autenticacao.

### 2.5 Encadeamento das decisoes

```mermaid
flowchart LR
  A["ADR-001: Microsservicos"] --> B["ADR-002: Event-Driven + Outbox"]
  B --> C["ADR-003: CQRS + Database per Service"]
  C --> D["ADR-004: Gateway + Keycloak"]
```

---

## 3. Requisitos nao funcionais

Escalabilidade:

- Servicos stateless com escalonamento horizontal (API e workers).
- Filas por evento e processamento assincrono para backpressure.
- Read models otimizados (Redis, MongoDB, DynamoDB) para consultas rapidas.
- Politicas de resiliencia (retry, circuit breaker, bulkhead, timeout) via `Cashflow.Shared.Resilience`.
- Meta operacional validada por carga: `50 req/s` com ate `5%` de perda (`http_req_failed <= 0.05`) e latencia `p95 <= 1500 ms`.

Resiliencia:

- Outbox Pattern para evitar falha parcial entre banco e broker.
- Consumidores idempotentes e controle de reentrega.
- DLQ e retry com atraso configuravel por consumidor (RabbitMQ).
- Recuperacao automatica de conexao no cliente RabbitMQ (`AutomaticRecoveryEnabled` + `TopologyRecoveryEnabled`).
- Isolamento por servico e por fila para evitar falhas em cascata.

Disponibilidade:

- Independencia entre servicos: falha de um worker nao bloqueia os demais.
- Gateway e API podem evoluir sem downtime dos workers.
- Endpoints de saude (`/health/live` e `/health/ready`) no Gateway e na Transaction API.
- Arquitetura preparada para multi-az e multi-cloud com configuracao externa.

Seguranca e observabilidade:

- Autenticacao centralizada via Keycloak (OIDC/OAuth2).
- Autorizacao por politica de escopo/role no write path (`transactions.write` / `transactions.writer`).
- Rate limiting no Gateway e na Transaction API para protecao de borda.
- CorrelationId propagado em toda a cadeia de eventos.
- Logs estruturados e rastreio distribuido com OpenTelemetry.

---

## 4. Integracao entre componentes

- Comunicacao assincrona via eventos (mensageria com envelopes e metadados).
- Outbox Worker publica eventos de dominio de forma confiavel.
- Saga Pattern coordena etapas com compensacoes em caso de falha.
- Versionamento de eventos protege contratos sem breaking changes.
- Validacao de integracao robusta por teste: fan-out entre consumidores independentes e envio para DLQ apos retries.

A integracao real ocorre exclusivamente por mensageria. Chamadas sincronas ficam restritas ao Gateway -> Transaction API, preservando desacoplamento entre workers.

---

## 5. Stack tecnologica

```
Backend        .NET 10 | ASP.NET Core Web API | C#
Seguranca      Keycloak (OIDC / OAuth2)
Mensageria     RabbitMQ (local) + abstracoes multicloud
Containers     Docker | Docker Compose
Testes         xUnit | Testcontainers | Pact | k6
CI/CD          GitHub Actions
```

---

## 6. Arquitetura e diagramas

### 6.1 Diagrama de containers (runtime)

```mermaid
flowchart TB
  subgraph Edge
    C["Client"]
    G["Gateway (YARP)"]
    K["Keycloak (OIDC)"]
    C --> G
    G <--> K
  end

  subgraph WriteSide
    T["Transaction API"]
    P[("Postgres")]
    O["Outbox Worker"]
    R["RabbitMQ"]
    G --> T
    T --> P
    O --> P
    O --> R
  end

  subgraph ReadSide
    BW["Balance Worker"]
    RW["Report Worker"]
    AW["Audit Worker"]
    Redis[("Redis")]
    MongoR[("Mongo Report")]
    MongoA[("Mongo Audit")]

    R --> BW --> Redis
    R --> RW --> MongoR
    R --> AW --> MongoA
  end
```

### 6.2 Diagrama de isolamento de falhas (servico independente)

```mermaid
flowchart LR
  TX["Transaction API (online)"] --> OB["Outbox"]
  OB --> MQ["RabbitMQ"]
  MQ --> REP["Report Worker (online)"]
  MQ --> AUD["Audit Worker (online)"]
  MQ -. atraso localizado .-> BAL["Balance Worker (down)"]
```

### 6.3 Referencias complementares

- Arquitetura detalhada: `docs/architecture.md`
- Runbook de carga (k6): `Back.End/Tests/Performance/README.md`
- Configuracao de ambiente/compose: `docs/docker-compose-config.md`

---

## 7. Fluxo principal

```mermaid
sequenceDiagram
  participant C  as Client
  participant G  as API Gateway
  participant T  as Transaction API
  participant W  as Write DB
  participant O  as OutboxEvents
  participant OW as Outbox Worker
  participant MB as Message Broker
  participant BW as Balance Worker
  participant RW as Report Worker
  participant AW as Audit Worker

  C->>G: POST /api/transactions
  G->>T: Forward request (+ Bearer token)
  T->>W: BEGIN - Save Transaction
  T->>O: BEGIN - Save OutboxEvent
  Note over W,O: Commit atomico - tudo ou nada
  OW->>O: Poll (WHERE ProcessedAt IS NULL)
  OW->>MB: Publish TransactionCreated
  OW->>O: UPDATE ProcessedAt = NOW()
  MB->>BW: Consume TransactionCreated
  MB->>RW: Consume TransactionCreated
  MB->>AW: Consume TransactionCreated
```

---

## 8. Versionamento de eventos

Eventos sao **contratos imutaveis**. Novas versoes sao adicionadas em paralelo sem quebrar consumidores existentes.

```
Cashflow.Shared.Events/
  Transactions/
    v1/TransactionCreatedEvent.cs
    v2/TransactionCreatedEvent.cs
```

Regras de evolucao:

- Nunca remover campos em versoes existentes
- Novos campos obrigatorios exigem nova versao
- Consumidores podem optar por escutar v1, v2 ou ambas
- O `EventType` publicado inclui a versao

---

## 9. Testes e qualidade

Tipos e objetivos:

- Unitarios: regras de dominio e validacoes puras
- Integracao: bancos, mensageria e gateway de autenticacao
- E2E: pipeline completo de eventos e read models
- Contract: compatibilidade entre Gateway e Transaction API

Novidade: testes de integracao do Gateway com Keycloak garantem autenticacao real por OIDC.

Como rodar testes (exemplos):

```bash
# Gateway + Keycloak
 dotnet test Back.End/Tests/IntegrationTests/Gateway/Gateway.Integration.Tests.csproj

# E2E completo
 dotnet test Back.End/Tests/E2E
```

Teste de carga NFR (Passo 3):

```bash
# sobe stack principal
docker compose up -d

# executa perfil de carga (k6)
docker compose --profile perf run --rm k6
```

Evidencia gerada:

- `Back.End/Tests/Performance/results/transactions-throughput-summary.json`
- Wrapper para Test Explorer (Visual Studio): `Back.End/Tests/Performance/k6/K6.Performance.Tests.csproj`
- Cenario NFR aprofundado: indisponibilidade do `balance-worker` sob carga com disponibilidade do write path.
- Integracao de mensageria aprofundada: `Back.End/Tests/IntegrationTests/Messaging/RabbitMqDecouplingIntegrationTests.cs`
- Seguranca de borda validada em integracao (401/403/201): `Back.End/Tests/IntegrationTests/Holistic/HolisticIntegrationTests.cs`
- Recuperacao de pipeline apos reinicio do Outbox Worker: `Back.End/Tests/IntegrationTests/Holistic/HolisticIntegrationTests.cs`
- Health endpoints validados em integracao: `Back.End/Tests/IntegrationTests/Holistic/HolisticIntegrationTests.cs`
- Gates de qualidade (Acao 7): `docs/tests-quality-gates.md`
- Matriz holistica 1-8 (Acao 8): `docs/holistic-execution-matrix.md`
- Runner unico de validacao holistica: `Back.End/Tests/run-holistic-validation.ps1`
- SonarQube local + script de analise: `scripts/sonarqube-local.ps1`
- Guia de qualidade com SonarQube: `docs/sonarqube-code-smells.md`

---

## 10. Execucao local

Subir infraestrutura:

```bash
docker compose up -d
```

Servicos principais:

- Gateway: `http://localhost:5000`
- Transaction API: `http://localhost:5001`
- Keycloak: `http://localhost:8081`

Execucao de carga com perfil dedicado:

- `docker compose --profile perf run --rm k6`

---

## 11. Estrutura da solution

```
Cashflow.slnx
  Back.End/
    Gateway -> Cashflow.Gateway
    Outbox/Worker -> Cashflow.Outbox.Worker
    Service/Transaction (API, Application, Domain, Infrastructure)
    Worker (Balance, Report, Audit)
    Shared (Events, Messaging, Logging, Resilience, Contracts)
    Tests
      ContractTests/Gateway
      IntegrationTests/Gateway
      IntegrationTests/Messaging
      IntegrationTests/Transaction
      IntegrationTests/Worker
      DomainTests
      ConcurrencyTests
      E2E
      Shared
```

---

## 12. CI/CD

Pipeline atual:

- Restore e build
- Testes unitarios, integracao e contract
- Build de imagens Docker
- Workflow de qualidade estatica SonarQube: `.github/workflows/sonarqube-analysis.yml`

---

## 13. SonarQube (Code Smells)

Subir SonarQube local:

```bash
docker compose -f docker-compose.sonarqube.yml up -d
```

Executar analise:

```powershell
$env:SONAR_TOKEN = "SEU_TOKEN"
powershell -ExecutionPolicy Bypass -File .\scripts\sonarqube-local.ps1 -ProjectKey "cashflow" -ProjectName "Cashflow"
```

Detalhes completos:
- `docs/sonarqube-code-smells.md`
- Workflow cloud: `.github/workflows/sonarqube-analysis.yml` (usa secrets `SONAR_HOST_URL` + `SONAR_TOKEN`)

---

## 14. Roadmap

- Exposicao de dados via API (queries otimizadas)
- Front-end minimo para exibicao
- Migracao do `docker compose` para Kubernetes

---

Licenca: Projeto de autoria de Antonio Leonardo.
