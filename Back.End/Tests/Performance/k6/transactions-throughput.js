import http from "k6/http";
import { check } from "k6";

const mode = (__ENV.MODE || "transactions").toLowerCase();

// Base URL para requests do cenário (atual para transacoes ou para consolidated daily).
const baseUrl = (__ENV.BASE_URL || "http://localhost:5001").replace(/\/+$/, "");
const authToken = __ENV.AUTH_TOKEN || "";

// Modo transactions (write path)
const transactionsEndpoint = __ENV.TRANSACTIONS_ENDPOINT || "/api/transactions";

// Modo daily-balance (read path)
const dailyBalanceEndpointTemplate = __ENV.DAILY_BALANCE_ENDPOINT_TEMPLATE || "/api/balance/daily/{accountId}?date={date}";

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

export const options = {
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

  for (const accountId of accounts) {
    const payload = JSON.stringify({
      AccountId: accountId,
      Amount: primeAmount,
      Currency: primeCurrency,
      Type: primeType,
    });

    // Best-effort priming; the load assertions will be strict on request success.
    http.post(`${primeBaseUrl}${transactionsEndpoint}`, payload, {
      headers,
      timeout: "10s",
    });
  }

  sleep(primeWaitMs / 1000);
  return { accounts, today };
}

export default function (data) {
  const headers = { "X-Correlation-Id": pseudoUuidV4() };

  if (authToken) {
    headers.Authorization = `Bearer ${authToken}`;
  }

  let response;

  if (mode === "daily-balance") {
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
