import http from "k6/http";
import { check } from "k6";

const baseUrl = (__ENV.BASE_URL || "http://localhost:5001").replace(/\/+$/, "");
const endpoint = __ENV.ENDPOINT || "/api/transactions";
const authToken = __ENV.AUTH_TOKEN || "";

const targetRps = Number(__ENV.TARGET_RPS || 50);
const duration = __ENV.DURATION || "60s";
const preAllocatedVUs = Number(__ENV.PRE_ALLOCATED_VUS || 100);
const maxVUs = Number(__ENV.MAX_VUS || 300);
const latencyP95Ms = Number(__ENV.LATENCY_P95_MS || 1500);

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

export default function () {
  const headers = {
    "Content-Type": "application/json",
    "X-Correlation-Id": pseudoUuidV4(),
  };

  if (authToken) {
    headers.Authorization = `Bearer ${authToken}`;
  }

  const payload = JSON.stringify({
    AccountId: pseudoUuidV4(),
    Amount: 100,
    Currency: "BRL",
    Type: 1,
  });

  const response = http.post(`${baseUrl}${endpoint}`, payload, {
    headers,
    timeout: "10s",
  });

  check(response, {
    "status is 201": (r) => r.status === 201,
  });
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
    "results/transactions-throughput-summary.json": JSON.stringify(summary, null, 2),
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
