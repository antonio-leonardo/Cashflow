using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using StackExchange.Redis;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace K6.Performance.Tests
{
    public class K6ThroughputE2ETests
    {
        private const string TransactionSummaryRelativePath = "Back.End/Tests/Performance/results/transactions-throughput-summary.json";
        private const string DailyBalanceSummaryRelativePath = "Back.End/Tests/Performance/results/daily-balance-throughput-summary.json";
        private const string HotAccountSummaryRelativePath = "Back.End/Tests/Performance/results/hot-account-throughput-summary.json";
        private const string KeepStackEnvVar = "KEEP_CASHFLOW_STACK";
        private const string CleanupVolumesEnvVar = "CLEANUP_CASHFLOW_VOLUMES";
        private const double MaxLossRate = 0.05;
        private const double LatencyP95ThresholdMs = 1500;
        private const string SetupTimeout = "2m";
        private const string RedisHostConnectionString = "localhost:6379";
        private const decimal HotAccountTransactionAmount = 100m;
        private const int HotAccountTransactionType = 1;
        private static readonly TimeSpan HotAccountConsistencyTimeout = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan HotAccountConsistencyPollInterval = TimeSpan.FromSeconds(2);

        private readonly ITestOutputHelper _output;

        public K6ThroughputE2ETests(ITestOutputHelper output)
        {
            _output = output;
        }

    [Fact]
        [Trait("Category", "Performance")]
        [Trait("Suite", "K6")]
        public async Task TransactionApi_Should_Handle_50Rps_With_Max_5Percent_Loss()
        {
            if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
            {
                _output.WriteLine("Performance tests are skipped in CI. Run them explicitly in a performance stage.");
                return;
            }

            var repositoryRoot = ResolveRepositoryRoot();
            var summaryFilePath = Path.Combine(repositoryRoot, TransactionSummaryRelativePath.Replace('/', Path.DirectorySeparatorChar));

            EnsureSummaryDirectoryExists(summaryFilePath);

            try
            {
                await RunK6AndAssertTargetsAsync(repositoryRoot, summaryFilePath);
            }
            finally
            {
                await CleanupDockerComposeAsync(repositoryRoot);
            }
        }

    [Fact]
        [Trait("Category", "Performance")]
        [Trait("Suite", "NFRHotAccount")]
        public async Task TransactionApi_HotAccount_Should_Handle_50Rps_With_Max_5Percent_Loss()
        {
            if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
            {
                _output.WriteLine("Performance tests are skipped in CI. Run them explicitly in a performance stage.");
                return;
            }

            var repositoryRoot = ResolveRepositoryRoot();
            var summaryFilePath = Path.Combine(repositoryRoot, HotAccountSummaryRelativePath.Replace('/', Path.DirectorySeparatorChar));

            EnsureSummaryDirectoryExists(summaryFilePath);

            try
            {
                var upResult = await RunDockerComposeCommandAsync(
                    repositoryRoot,
                    "compose up -d transaction-api worker-outbox balance-worker report-worker audit-worker redis rabbitmq postgres",
                    TimeSpan.FromMinutes(6));

                _output.WriteLine(upResult.Output);

                Assert.True(
                    upResult.ExitCode == 0,
                    $"Unable to start infrastructure for hot-account scenario.{Environment.NewLine}{upResult.Output}");

                await EnsureTransactionApiReadyAsync(repositoryRoot);
                await EnsureWorkerQueuesReadyAsync();

                DeleteExistingSummaryFile(summaryFilePath);

                var hotAccountId = Guid.NewGuid();

                var result = await RunDockerComposeCommandAsync(
                    repositoryRoot,
                    BuildHotAccountK6RunArguments(
                        targetRps: 50,
                        durationSeconds: 60,
                        hotAccountId: hotAccountId),
                    TimeSpan.FromMinutes(8));

                _output.WriteLine($"[hot-account] AccountId under load: {hotAccountId}");
                _output.WriteLine(result.Output);

                Assert.True(
                    result.ExitCode == 0,
                    $"k6 hot-account execution failed with exit code {result.ExitCode}.{Environment.NewLine}{result.Output}");

                Assert.True(
                    File.Exists(summaryFilePath),
                    $"Summary file was not generated at '{summaryFilePath}'.");

                var summary = await ReadSummaryAsync(summaryFilePath);
                Assert.NotNull(summary);
                Assert.Equal("PASS", summary!.Result);
                Assert.True(
                    summary.FailedRate <= MaxLossRate,
                    $"Expected loss <= {MaxLossRate * 100:F0}%, got {(summary.FailedRate * 100):F2}%.");
                Assert.True(
                    summary.P95DurationMs <= LatencyP95ThresholdMs,
                    $"Expected p95 latency <= {LatencyP95ThresholdMs:F0} ms, got {summary.P95DurationMs:F2} ms.");

                Assert.True(
                    summary.ChecksPassed > 0,
                    $"Expected at least one accepted request in hot-account run, got {summary.ChecksPassed}.");

                var signedAmountPerRequest = GetSignedAmountByTransactionType(
                    HotAccountTransactionAmount,
                    HotAccountTransactionType);
                var expectedBalance = summary.ChecksPassed * signedAmountPerRequest;
                _output.WriteLine(
                    $"[hot-account] Accepted requests={summary.ChecksPassed}, expected consolidated balance={expectedBalance.ToString(CultureInfo.InvariantCulture)} (type={HotAccountTransactionType}).");

                await AssertHotAccountConsolidatedBalanceAsync(hotAccountId, expectedBalance);
            }
            finally
            {
                await CleanupDockerComposeAsync(repositoryRoot);
            }
        }

    [Fact]
        [Trait("Category", "Performance")]
        [Trait("Suite", "NfrAprofundado")]
        public async Task TransactionApi_Should_Stay_Available_Under_Load_When_BalanceWorker_Is_Down()
        {
            if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
            {
                _output.WriteLine("Performance tests are skipped in CI. Run them explicitly in a performance stage.");
                return;
            }

            var repositoryRoot = ResolveRepositoryRoot();
            var summaryFilePath = Path.Combine(repositoryRoot, TransactionSummaryRelativePath.Replace('/', Path.DirectorySeparatorChar));

            EnsureSummaryDirectoryExists(summaryFilePath);

            try
            {
                var upResult = await RunDockerComposeCommandAsync(
                    repositoryRoot,
                    "compose up -d transaction-api worker-outbox balance-worker report-worker audit-worker",
                    TimeSpan.FromMinutes(6));

                _output.WriteLine(upResult.Output);

                Assert.True(
                    upResult.ExitCode == 0,
                    $"Unable to start infrastructure for degraded scenario.{Environment.NewLine}{upResult.Output}");

                var stopBalanceResult = await RunDockerComposeCommandAsync(
                    repositoryRoot,
                    "compose stop balance-worker",
                    TimeSpan.FromMinutes(2));

                _output.WriteLine(stopBalanceResult.Output);

                Assert.True(
                    stopBalanceResult.ExitCode == 0,
                    $"Unable to stop balance-worker for degraded scenario.{Environment.NewLine}{stopBalanceResult.Output}");

                await RunK6AndAssertTargetsAsync(repositoryRoot, summaryFilePath);
            }
            finally
            {
                await CleanupDockerComposeAsync(repositoryRoot);
            }
        }

    [Fact]
        [Trait("Category", "Performance")]
        [Trait("Suite", "NFRDailyConsolidatedBalance")]
        public async Task BalanceDailyApi_Should_Handle_50Rps_With_Max_5Percent_Loss()
        {
            if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
            {
                _output.WriteLine("Performance tests are skipped in CI. Run them explicitly in a performance stage.");
                return;
            }

            var repositoryRoot = ResolveRepositoryRoot();
            var summaryFilePath = Path.Combine(repositoryRoot, DailyBalanceSummaryRelativePath.Replace('/', Path.DirectorySeparatorChar));

            EnsureSummaryDirectoryExists(summaryFilePath);

            try
            {
                var upResult = await RunDockerComposeCommandAsync(
                    repositoryRoot,
                    "compose up -d transaction-api worker-outbox balance-worker report-worker audit-worker balance-query-api redis rabbitmq postgres",
                    TimeSpan.FromMinutes(6));

                _output.WriteLine(upResult.Output);

                Assert.True(
                    upResult.ExitCode == 0,
                    $"Unable to start infrastructure for daily consolidated scenario.{Environment.NewLine}{upResult.Output}");

                await EnsureBalanceQueryApiReadyAsync(repositoryRoot);
                await EnsureWorkerQueuesReadyAsync();

                DeleteExistingSummaryFile(summaryFilePath);

                // Use a prime set bounded in time (k6 setupTimeout is limited).
                const int primeAccounts = 20;
                const int primeWaitMs = 8000;
                const int primeMaxWaitSeconds = 70;
                const int primePollIntervalSeconds = 1;
                const int minReadyAccounts = 5;
                const int primeWarmupMs = 15000;

                var result = await RunDockerComposeCommandAsync(
                    repositoryRoot,
                    BuildK6RunArguments(
                        targetRps: 50,
                        durationSeconds: 60,
                        mode: "daily-balance",
                        baseUrl: "http://balance-query-api:8080",
                        primeBaseUrl: "http://transaction-api:8080",
                        summaryFile: "daily-balance-throughput-summary.json",
                        primeAccounts: primeAccounts,
                        primeWaitMs: primeWaitMs,
                        primeMaxWaitSeconds: primeMaxWaitSeconds,
                        primePollIntervalSeconds: primePollIntervalSeconds,
                        minReadyAccounts: minReadyAccounts,
                        primeWarmupMs: primeWarmupMs),
                    TimeSpan.FromMinutes(8));

                _output.WriteLine(result.Output);

                Assert.True(
                    result.ExitCode == 0,
                    $"k6 execution failed with exit code {result.ExitCode}.{Environment.NewLine}{result.Output}");

                Assert.True(
                    File.Exists(summaryFilePath),
                    $"Summary file was not generated at '{summaryFilePath}'.");

                var summary = await ReadSummaryAsync(summaryFilePath);
                Assert.NotNull(summary);
                Assert.Equal("PASS", summary!.Result);
                Assert.True(
                    summary.FailedRate <= MaxLossRate,
                    $"Expected loss <= {MaxLossRate * 100:F0}%, got {(summary.FailedRate * 100):F2}%.");
                Assert.True(
                    summary.P95DurationMs <= LatencyP95ThresholdMs,
                    $"Expected p95 latency <= {LatencyP95ThresholdMs:F0} ms, got {summary.P95DurationMs:F2} ms.");
            }
            finally
            {
                await CleanupDockerComposeAsync(repositoryRoot);
            }
        }

        private static void EnsureSummaryDirectoryExists(string summaryFilePath)
        {
            var summaryDirectory = Path.GetDirectoryName(summaryFilePath);

            if (string.IsNullOrWhiteSpace(summaryDirectory))
            {
                throw new InvalidOperationException("Unable to resolve k6 summary directory.");
            }

            Directory.CreateDirectory(summaryDirectory);
        }

        private static void DeleteExistingSummaryFile(string summaryFilePath)
        {
            if (File.Exists(summaryFilePath))
            {
                File.Delete(summaryFilePath);
            }
        }

        private static async Task<K6Summary?> ReadSummaryAsync(string summaryFilePath)
        {
            await using var stream = File.OpenRead(summaryFilePath);

            return await JsonSerializer.DeserializeAsync<K6Summary>(
                stream,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }

        private async Task RunK6AndAssertTargetsAsync(string repositoryRoot, string summaryFilePath)
        {
            await EnsureTransactionApiReadyAsync(repositoryRoot);
            DeleteExistingSummaryFile(summaryFilePath);

            var result = await RunDockerComposeCommandAsync(
                repositoryRoot,
                BuildK6RunArguments(targetRps: 50, durationSeconds: 10),
                TimeSpan.FromMinutes(8));

            _output.WriteLine(result.Output);

            Assert.True(
                result.ExitCode == 0,
                $"k6 execution failed with exit code {result.ExitCode}.{Environment.NewLine}{result.Output}");

            Assert.True(
                File.Exists(summaryFilePath),
                $"Summary file was not generated at '{summaryFilePath}'.");

            var summary = await ReadSummaryAsync(summaryFilePath);

            Assert.NotNull(summary);
            Assert.Equal("PASS", summary!.Result);
            Assert.True(
                summary.FailedRate <= MaxLossRate,
                $"Expected loss <= {MaxLossRate * 100:F0}%, got {(summary.FailedRate * 100):F2}%.");
            Assert.True(
                summary.P95DurationMs <= LatencyP95ThresholdMs,
                $"Expected p95 latency <= {LatencyP95ThresholdMs:F0} ms, got {summary.P95DurationMs:F2} ms.");
        }

        private async Task EnsureTransactionApiReadyAsync(string repositoryRoot)
        {
            var upResult = await RunDockerComposeCommandAsync(
                repositoryRoot,
                "compose up -d transaction-api",
                TimeSpan.FromMinutes(4));

            _output.WriteLine(upResult.Output);

            Assert.True(
                upResult.ExitCode == 0,
                $"Unable to ensure transaction-api startup.{Environment.NewLine}{upResult.Output}");

            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(2)
            };

            var deadline = DateTime.UtcNow.AddMinutes(2);

            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    using var response = await client.GetAsync("http://localhost:5001/api/v1/transactions");

                    if ((int)response.StatusCode >= 100)
                    {
                        return;
                    }
                }
                catch
                {
                    // retry until timeout
                }

                await Task.Delay(1000);
            }

            throw new XunitException("Transaction API did not become reachable on http://localhost:5001 within timeout.");
        }

        private static string BuildK6RunArguments(
            int targetRps,
            int durationSeconds,
            string mode,
            string baseUrl,
            string primeBaseUrl,
            string summaryFile,
            int primeAccounts,
            int primeWaitMs,
            int primeMaxWaitSeconds,
            int primePollIntervalSeconds,
            int minReadyAccounts,
            int primeWarmupMs)
            => $"compose --profile perf run --rm " +
               $"-e MODE={mode} " +
               $"-e BASE_URL={baseUrl} " +
               $"-e PRIME_BASE_URL={primeBaseUrl} " +
               $"-e TRANSACTIONS_ENDPOINT=/api/v1/transactions " +
               $"-e DAILY_BALANCE_ENDPOINT_TEMPLATE=/api/v1/balance/daily/{{accountId}}?date={{date}} " +
               $"-e PRIME_WAIT_MS={primeWaitMs} " +
               $"-e PRIME_ACCOUNTS={primeAccounts} " +
               $"-e PRIME_MAX_WAIT_SECONDS={primeMaxWaitSeconds} " +
               $"-e PRIME_POLL_INTERVAL_SECONDS={primePollIntervalSeconds} " +
               $"-e MIN_READY_ACCOUNTS={minReadyAccounts} " +
               $"-e PRIME_WARMUP_MS={primeWarmupMs} " +
               $"-e TARGET_RPS={targetRps} " +
               $"-e DURATION={durationSeconds}s " +
               $"-e PRE_ALLOCATED_VUS=120 " +
               $"-e MAX_VUS=400 " +
               $"-e LATENCY_P95_MS={LatencyP95ThresholdMs:F0} " +
               $"-e SETUP_TIMEOUT={SetupTimeout} " +
               $"-e SUMMARY_FILE=results/{summaryFile} " +
               $"k6 run k6/transactions-throughput.js";

        private static string BuildK6RunArguments(int targetRps, int durationSeconds)
            => $"compose --profile perf run --rm -e TARGET_RPS={targetRps} -e DURATION={durationSeconds}s -e PRE_ALLOCATED_VUS=120 -e MAX_VUS=400 -e LATENCY_P95_MS={LatencyP95ThresholdMs:F0} k6 run k6/transactions-throughput.js";

        private static string BuildHotAccountK6RunArguments(int targetRps, int durationSeconds, Guid hotAccountId)
            => $"compose --profile perf run --rm " +
               $"-e MODE=hot-account " +
               $"-e HOT_ACCOUNT_ID={hotAccountId:D} " +
               $"-e HOT_ACCOUNT_AMOUNT={HotAccountTransactionAmount.ToString(CultureInfo.InvariantCulture)} " +
               $"-e HOT_ACCOUNT_TYPE={HotAccountTransactionType} " +
               $"-e BASE_URL=http://transaction-api:8080 " +
               $"-e TRANSACTIONS_ENDPOINT=/api/v1/transactions " +
               $"-e TARGET_RPS={targetRps} " +
               $"-e DURATION={durationSeconds}s " +
               $"-e PRE_ALLOCATED_VUS=120 " +
               $"-e MAX_VUS=400 " +
               $"-e LATENCY_P95_MS={LatencyP95ThresholdMs:F0} " +
               $"-e SUMMARY_FILE=results/hot-account-throughput-summary.json " +
               $"k6 run k6/transactions-throughput.js";

        private static decimal GetSignedAmountByTransactionType(decimal amount, int transactionType)
        {
            // Domain enum: 0=Credit, 1=Debit.
            return transactionType == 0 ? amount : -amount;
        }

        private async Task AssertHotAccountConsolidatedBalanceAsync(Guid accountId, decimal expectedBalance)
        {
            var balanceKey = $"balance:{accountId:D}";
            await using var redis = await ConnectionMultiplexer.ConnectAsync(RedisHostConnectionString);
            var db = redis.GetDatabase();
            var deadline = DateTime.UtcNow.Add(HotAccountConsistencyTimeout);

            RedisValue lastRawValue = RedisValue.Null;
            decimal lastParsedValue = 0m;
            var hasParsedValue = false;

            while (DateTime.UtcNow < deadline)
            {
                lastRawValue = await db.StringGetAsync(balanceKey);

                if (!lastRawValue.IsNullOrEmpty &&
                    decimal.TryParse(
                        lastRawValue.ToString(),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var observedBalance))
                {
                    hasParsedValue = true;
                    lastParsedValue = observedBalance;

                    if (observedBalance == expectedBalance)
                    {
                        _output.WriteLine(
                            $"[hot-account] Consolidated balance confirmed for {accountId:D}: {observedBalance.ToString(CultureInfo.InvariantCulture)}.");
                        return;
                    }
                }

                await Task.Delay(HotAccountConsistencyPollInterval);
            }

            var observedDisplay = hasParsedValue
                ? lastParsedValue.ToString(CultureInfo.InvariantCulture)
                : (lastRawValue.IsNullOrEmpty ? "<null>" : lastRawValue.ToString());

            throw new XunitException(
                $"Hot-account consolidated balance mismatch for account {accountId:D}. " +
                $"Expected {expectedBalance.ToString(CultureInfo.InvariantCulture)} based on accepted requests, " +
                $"observed {observedDisplay} after waiting {HotAccountConsistencyTimeout.TotalSeconds:F0}s.");
        }

        private async Task EnsureWorkerQueuesReadyAsync()
        {
            var workerQueues = new[]
            {
                "balance.TransactionCreatedEventV1",
                "audit.TransactionCreatedEventV1",
                "report.TransactionCreatedEventV1"
            };

            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes("guest:guest"));

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            client.DefaultRequestHeaders.Add("Authorization", $"Basic {credentials}");

            var deadline = DateTime.UtcNow.AddMinutes(2);

            foreach (var queue in workerQueues)
            {
                var confirmed = false;
                var encodedQueue = Uri.EscapeDataString(queue);

                while (DateTime.UtcNow < deadline)
                {
                    try
                    {
                        var response = await client.GetAsync(
                            $"http://localhost:15672/api/queues/%2F/{encodedQueue}");

                        if (response.StatusCode == System.Net.HttpStatusCode.OK)
                        {
                            _output.WriteLine($"[worker-ready] Queue '{queue}' confirmed in RabbitMQ.");
                            confirmed = true;
                            break;
                        }
                    }
                    catch
                    {
                        // Management API ainda não disponível — retry
                    }

                    await Task.Delay(1000);
                }

                if (!confirmed)
                {
                    throw new XunitException(
                        $"Worker queue '{queue}' was not found in RabbitMQ within 2 minutes. " +
                        $"The worker may have failed to start or SubscribeAsync did not complete.");
                }
            }
        }

        private async Task EnsureBalanceQueryApiReadyAsync(string repositoryRoot)
        {
            // balance-query-api is available via published ports on the host while running tests.
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(2)
            };

            var deadline = DateTime.UtcNow.AddMinutes(2);

            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    var response = await client.GetAsync("http://localhost:5002/health/ready");
                    if ((int)response.StatusCode >= 100 && (int)response.StatusCode < 500)
                    {
                        if (response.StatusCode == System.Net.HttpStatusCode.OK)
                        {
                            return;
                        }
                    }
                }
                catch
                {
                    // retry until timeout
                }

                await Task.Delay(1000);
            }

            throw new XunitException("Balance Query API did not become reachable on http://localhost:5002/health/ready within timeout.");
        }

        private async Task CleanupDockerComposeAsync(string repositoryRoot)
        {
            if (string.Equals(Environment.GetEnvironmentVariable(KeepStackEnvVar), "true", StringComparison.OrdinalIgnoreCase))
            {
                _output.WriteLine($"Skipping docker compose cleanup because {KeepStackEnvVar}=true.");
                return;
            }

            var removeVolumes = string.Equals(
                Environment.GetEnvironmentVariable(CleanupVolumesEnvVar),
                "true",
                StringComparison.OrdinalIgnoreCase);

            var downArgs = removeVolumes
                ? "compose --profile perf down -v --remove-orphans"
                : "compose --profile perf down --remove-orphans";

            var cleanupResult = await RunDockerComposeCommandAsync(repositoryRoot, downArgs, TimeSpan.FromMinutes(3));

            _output.WriteLine(cleanupResult.Output);

            Assert.True(
                cleanupResult.ExitCode == 0,
                $"docker compose cleanup failed with exit code {cleanupResult.ExitCode}.{Environment.NewLine}{cleanupResult.Output}");
        }

        private static async Task<CommandResult> RunDockerComposeCommandAsync(
            string workingDirectory,
            string commandArguments,
            TimeSpan timeout)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = commandArguments,
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.StartInfo.Environment.Remove("DOCKER_HOST");
            process.StartInfo.Environment.Remove("DOCKER_CONTEXT");

            var outputBuilder = new StringBuilder();

            process.OutputDataReceived += (_, args) =>
            {
                if (args.Data is not null)
                {
                    outputBuilder.AppendLine(args.Data);
                }
            };

            process.ErrorDataReceived += (_, args) =>
            {
                if (args.Data is not null)
                {
                    outputBuilder.AppendLine(args.Data);
                }
            };

            try
            {
                if (!process.Start())
                {
                    throw new InvalidOperationException("Failed to start docker process.");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Docker is not available in PATH. Details: {ex.Message}");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var exited = await Task.Run(() => process.WaitForExit((int)timeout.TotalMilliseconds));

            if (!exited)
            {
                process.Kill(true);
                return new CommandResult(
                    ExitCode: -1,
                    Output: $"Timeout running docker compose k6 after {timeout.TotalMinutes:F0} minutes.");
            }

            return new CommandResult(process.ExitCode, outputBuilder.ToString());
        }

        private static string ResolveRepositoryRoot()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);

            while (current is not null)
            {
                var dockerComposePath = Path.Combine(current.FullName, "docker-compose.yml");

                if (File.Exists(dockerComposePath))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException(
                "Repository root could not be resolved (docker-compose.yml not found in parent chain).");
        }

        private sealed record CommandResult(int ExitCode, string Output);

        private sealed class K6Summary
        {
            public long TotalRequests { get; set; }
            public long ChecksPassed { get; set; }
            public long ChecksFailed { get; set; }
            public double PassedRate { get; set; }
            public double FailedRate { get; set; }
            public double P95DurationMs { get; set; }
            public string Result { get; set; } = string.Empty;
        }
    }
}
