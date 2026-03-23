using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace K6.Performance.Tests
{
    public class K6ThroughputE2ETests
    {
        private const string SummaryRelativePath = "Back.End/Tests/Performance/results/transactions-throughput-summary.json";
        private const string KeepStackEnvVar = "KEEP_CASHFLOW_STACK";
        private const string CleanupVolumesEnvVar = "CLEANUP_CASHFLOW_VOLUMES";
        private const double MaxLossRate = 0.05;
        private const double LatencyP95ThresholdMs = 1500;

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
            var summaryFilePath = Path.Combine(repositoryRoot, SummaryRelativePath.Replace('/', Path.DirectorySeparatorChar));

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
        [Trait("Suite", "NfrAprofundado")]
        public async Task TransactionApi_Should_Stay_Available_Under_Load_When_BalanceWorker_Is_Down()
        {
            if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
            {
                _output.WriteLine("Performance tests are skipped in CI. Run them explicitly in a performance stage.");
                return;
            }

            var repositoryRoot = ResolveRepositoryRoot();
            var summaryFilePath = Path.Combine(repositoryRoot, SummaryRelativePath.Replace('/', Path.DirectorySeparatorChar));

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
                    using var response = await client.GetAsync("http://localhost:5001/api/transactions");

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

        private static string BuildK6RunArguments(int targetRps, int durationSeconds)
            => $"compose --profile perf run --rm -e TARGET_RPS={targetRps} -e DURATION={durationSeconds}s -e PRE_ALLOCATED_VUS=120 -e MAX_VUS=400 -e LATENCY_P95_MS={LatencyP95ThresholdMs:F0} k6 run k6/transactions-throughput.js";

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
            public double FailedRate { get; set; }
            public double P95DurationMs { get; set; }
            public string Result { get; set; } = string.Empty;
        }
    }
}
