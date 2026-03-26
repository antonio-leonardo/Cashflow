# Performance Tests (Step 3)

This folder contains the load test harness for the NFR target:

- Throughput target: `50 req/s`
- Maximum accepted loss: `5%`

## Scenario implemented

- Script: `k6/transactions-throughput.js`
- Endpoint under load (default): `POST /api/v1/transactions`
- Endpoint under load (daily mode): `GET /api/v1/balance/daily/{accountId}?date=yyyy-MM-dd`
- Load profile: constant arrival rate (`constant-arrival-rate`)
- Default execution: `50 req/s` for `60s`
- Acceptance threshold: `http_req_failed <= 0.05`
- Latency guardrail: `http_req_duration p(95) <= 1500 ms`

## How to run (Docker + k6)

1. Start the stack:

```bash
docker compose up -d
```

2. Run load test:

```bash
docker compose --profile perf run --rm k6
```

3. Check summary artifact:

- `results/transactions-throughput-summary.json`

## Run from Visual Studio Test Explorer

The wrapper test project is versioned and portable:

- Project: `Back.End/Tests/Performance/k6/K6.Performance.Tests.csproj`
- Test: `TransactionApi_Should_Handle_50Rps_With_Max_5Percent_Loss`
- Test: `TransactionApi_Should_Stay_Available_Under_Load_When_BalanceWorker_Is_Down`
- Test: `BalanceDailyApi_Should_Handle_50Rps_With_Max_5Percent_Loss`

This test invokes docker compose + k6 and appears like any other xUnit test in Test Explorer.
After test execution it automatically runs `docker compose --profile perf down --remove-orphans` to release CPU/RAM.

Optional behavior flags:

- `KEEP_CASHFLOW_STACK=true` -> skip automatic cleanup.
- `CLEANUP_CASHFLOW_VOLUMES=true` -> cleanup with `-v` (also removes volumes/data).

## Optional overrides

Use environment variables to tune the run without changing the script:

```bash
TARGET_RPS=50 DURATION=120s PRE_ALLOCATED_VUS=120 MAX_VUS=400 docker compose --profile perf run --rm k6
```

For Gateway-authenticated runs, provide:

- `BASE_URL=http://gateway:8080`
- `AUTH_TOKEN=<bearer-token>`

## Notes

- This suite validates ingress loss/availability under peak throughput.
- Service-independence behavior remains validated by E2E tests in:
  - `Back.End/Tests/E2E/Balance/ServiceIndependenceE2ETests.cs`
  - `Back.End/Tests/E2E/Audit/ServiceIndependenceE2ETests.cs`
  - `Back.End/Tests/E2E/Report/ServiceIndependenceE2ETests.cs`
