import http from "k6/http";
import { check, sleep } from "k6";

const mode = (__ENV.MODE || "transactions").toLowerCase();

// Base URL para requests do cenário (atual para transacoes ou para consolidated daily).
const baseUrl = (__ENV.BASE_URL || "http://localhost:5001").replace(/\/+$/, "");
const authToken = __ENV.AUTH_TOKEN || "";

// Modo transactions (write path)
const transactionsEndpoint = __ENV.TRANSACTIONS_ENDPOINT || "/api/v1/transactions";

// Modo daily-balance (read path)
const dailyBalanceEndpointTemplate = __ENV.DAILY_BALANCE_ENDPOINT_TEMPLATE || "/api/v1/balance/daily/{accountId}?date={date}";
const hotAccountIdFromEnv = (__ENV.HOT_ACCOUNT_ID || "").trim();
const hotAccountAmount = Number(__ENV.HOT_ACCOUNT_AMOUNT || 100);
const hotAccountType = Number(__ENV.HOT_ACCOUNT_TYPE || 1);
const hotAccountCurrency = (__ENV.HOT_ACCOUNT_CURRENCY || "BRL");

const primeBaseUrl = (__ENV.PRIME_BASE_URL || "http://transaction-api:8080").replace(/\/+$/, "");
const primeWaitMs = Number(__ENV.PRIME_WAIT_MS || 5000);
const primeAccounts = Number(__ENV.PRIME_ACCOUNTS || 50);
const primeAmount = Number(__ENV.PRIME_AMOUNT || 100);
const primeType = Number(__ENV.PRIME_TYPE || 1);
const primeCurrency = (__ENV.PRIME_CURRENCY || "BRL");

const targetRps = Number(__ENV.TARGET_RPS || 50);
const duration = __ENV.DURATION || "60s";
const preAllocatedVUs = Number(__ENV.PRE_ALLOCATED_VUS || 100);
const maxVUs = Number(__ENV.MAX_VUS || 300);
const latencyP95Ms = Number(__ENV.LATENCY_P95_MS || 1500);
const summaryFile = __ENV.SUMMARY_FILE || "results/transactions-throughput-summary.json";
const setupTimeout = __ENV.SETUP_TIMEOUT || "2m";

export const options = {
  setupTimeout,
  discardResponseBodies: true,
  scenarios: {
    steady_50_rps: {
      executor: "constant-arrival-rate",
      rate: targetRps,
      timeUnit: "1s",
      duration,
      preAllocatedVUs,
      maxVUs,
    },
  },
  thresholds: {
    http_req_failed: ["rate<=0.05"],
    checks: ["rate>=0.95"],
    http_req_duration: [`p(95)<=${latencyP95Ms}`],
  },
};

export function setup() {
  if (mode === "hot-account") {
    return {
      hotAccountId: hotAccountIdFromEnv || pseudoUuidV4(),
      accounts: [],
      today: "",
    };
  }

  if (mode !== "daily-balance") {
    return { accounts: [], today: "" };
  }

  const today = (__ENV.DATE || new Date().toISOString().slice(0, 10));
  const accounts = [];

  // Pre-keys for the daily read model:
  // 1) create transactions for a fixed set of accounts
  // 2) wait some seconds for the asynchronous balance worker to materialize the daily snapshot
  for (let i = 0; i < primeAccounts; i++) {
    accounts.push(pseudoUuidV4());
  }

  const headers = {
    "Content-Type": "application/json",
    "X-Correlation-Id": pseudoUuidV4(),
  };

  if (authToken) {
    headers["Authorization"] = `Bearer ${authToken}`;
  }

  // Warm-up para garantir que os workers já iniciaram o Subscribe/Topology
  // no RabbitMQ antes de começarmos a publicar eventos via outbox.
  const warmupMs = Number(__ENV.PRIME_WARMUP_MS || 15000);
  sleep(warmupMs / 1000);

  const primeSucceededAccounts = [];
  for (const accountId of accounts) {
    const payload = JSON.stringify({
      AccountId: accountId,
      Amount: primeAmount,
      Currency: primeCurrency,
      Type: primeType,
    });

    const primeResp = http.post(`${primeBaseUrl}${transactionsEndpoint}`, payload, {
      headers,
      timeout: "10s",
    });

    if (primeResp.status === 201) {
      primeSucceededAccounts.push(accountId);
    } else {
      // Help debugging when priming fails (still keep it short to avoid huge logs).
      console.log(`[prime] accountId=${accountId} status=${primeResp.status}`);
    }
  }

  if (primeSucceededAccounts.length === 0) {
    throw new Error(`No priming transactions succeeded (all status != 201).`);
  }

  // Give the async pipeline a head start.
  sleep(primeWaitMs / 1000);

  // Deterministic gate: ensure the consolidated daily endpoint is already serving 200
  // for the accounts used during the load test. Otherwise we'd measure 404s from
  // eventual consistency instead of the API under load.
  const maxWaitSeconds = Number(__ENV.PRIME_MAX_WAIT_SECONDS || 75);
  const pollIntervalSeconds = Number(__ENV.PRIME_POLL_INTERVAL_SECONDS || 1);
  let minReadyAccounts = Number(__ENV.MIN_READY_ACCOUNTS || Math.min(10, primeAccounts));
  minReadyAccounts = Math.min(minReadyAccounts, primeSucceededAccounts.length);

  const deadline = Date.now() + maxWaitSeconds * 1000;
  const readyAccounts = [];
  const lastStatusByAccount = {};

  // Only consider accounts where priming succeeded.
  for (const accountId of primeSucceededAccounts) {
    if (readyAccounts.length >= minReadyAccounts) {
      break;
    }

    const path = dailyBalanceEndpointTemplate
      .replace("{accountId}", accountId)
      .replace("{date}", today);

    while (Date.now() < deadline) {
      const resp = http.get(`${baseUrl}${path}`, { headers, timeout: "10s" });
      lastStatusByAccount[accountId] = resp.status;
      if (resp.status === 200) {
        readyAccounts.push(accountId);
        break;
      }
      sleep(pollIntervalSeconds);
    }
  }

  if (readyAccounts.length < minReadyAccounts) {
    throw new Error(
      `Daily balance not ready for enough accounts: ready=${readyAccounts.length}, required=${minReadyAccounts}, today=${today}. lastStatusByAccount=${JSON.stringify(lastStatusByAccount)}`
    );
  }

  return { accounts: readyAccounts, today };
}

export default function (data) {
  const headers = { "X-Correlation-Id": pseudoUuidV4() };

  if (authToken) {
    headers.Authorization = `Bearer ${authToken}`;
  }

  let response;

  if (mode === "hot-account") {
    const payload = JSON.stringify({
      AccountId: data.hotAccountId,
      Amount: hotAccountAmount,
      Currency: hotAccountCurrency,
      Type: hotAccountType,
    });

    response = http.post(`${baseUrl}${transactionsEndpoint}`, payload, {
      headers: { ...headers, "Content-Type": "application/json" },
      timeout: "10s",
    });

    check(response, {
      "status is 201": (r) => r.status === 201,
    });
  } else if (mode === "daily-balance") {
    const accountId = data.accounts[Math.floor(Math.random() * data.accounts.length)];
    const date = data.today;

    const dailyPath = dailyBalanceEndpointTemplate
      .replace("{accountId}", accountId)
      .replace("{date}", date);

    response = http.get(`${baseUrl}${dailyPath}`, { headers, timeout: "10s" });

    check(response, {
      "status is 200": (r) => r.status === 200,
    });
  } else {
    // Default behavior (transactions write path)
    const payload = JSON.stringify({
      AccountId: pseudoUuidV4(),
      Amount: 100,
      Currency: "BRL",
      Type: 1,
    });

    response = http.post(`${baseUrl}${transactionsEndpoint}`, payload, {
      headers: { ...headers, "Content-Type": "application/json" },
      timeout: "10s",
    });

    check(response, {
      "status is 201": (r) => r.status === 201,
    });
  }
}

export function handleSummary(data) {
  const failedRate = data.metrics.http_req_failed?.values?.rate ?? 1;
  const checksRate = data.metrics.checks?.values?.rate ?? 0;
  const checksPassed = data.metrics.checks?.values?.passes ?? 0;
  const checksFailed = data.metrics.checks?.values?.fails ?? 0;
  const totalRequests = data.metrics.http_reqs?.values?.count ?? 0;
  const p95DurationMs = data.metrics.http_req_duration?.values?.["p(95)"] ?? Number.POSITIVE_INFINITY;
  const avgDurationMs = data.metrics.http_req_duration?.values?.avg ?? Number.POSITIVE_INFINITY;
  const passed =
    failedRate <= 0.05 &&
    checksRate >= 0.95 &&
    p95DurationMs <= latencyP95Ms;

  const summary = {
    targetRps,
    duration,
    totalRequests,
    checksPassed,
    checksFailed,
    failedRate,
    passedRate: checksRate,
    p95DurationMs,
    avgDurationMs,
    latencyP95ThresholdMs: latencyP95Ms,
    result: passed ? "PASS" : "FAIL",
  };

  return {
    [summaryFile]: JSON.stringify(summary, null, 2),
    stdout:
      "\n" +
      `[k6] Target: ${targetRps} req/s for ${duration}\n` +
      `[k6] Requests: ${totalRequests}\n` +
      `[k6] Failed rate: ${(failedRate * 100).toFixed(2)}% (threshold <= 5%)\n` +
      `[k6] Latency p95: ${p95DurationMs.toFixed(2)} ms (threshold <= ${latencyP95Ms} ms)\n` +
      `[k6] Latency avg: ${avgDurationMs.toFixed(2)} ms\n` +
      `[k6] Check pass rate: ${(checksRate * 100).toFixed(2)}%\n` +
      `[k6] Result: ${summary.result}\n`,
  };
}

function pseudoUuidV4() {
  return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, (char) => {
    const random = Math.floor(Math.random() * 16);
    const value = char === "x" ? random : (random & 0x3) | 0x8;
    return value.toString(16);
  });
}
