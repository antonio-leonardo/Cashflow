using System.Diagnostics;

namespace Infrastructure.Test
{
    internal static class DockerTestEnvironment
    {
        private static readonly string DockerCliConfigDirectory =
            Path.Combine(Path.GetTempPath(), "cashflow-docker-cli");
        private static readonly object SyncRoot = new();
        private static bool _dockerReady;

        public static void EnsureDockerIsReady()
        {
            if (_dockerReady)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (_dockerReady)
                {
                    return;
                }

                var lastError = WaitForDocker(TimeSpan.FromMinutes(2), TimeSpan.FromSeconds(2));
                if (lastError is null)
                {
                    _dockerReady = true;
                    return;
                }

                throw new InvalidOperationException(
                    $"Docker did not become ready within 120 seconds. Last error: {lastError}");
            }
        }

        public static Task EnsureDockerIsReadyAsync()
        {
            EnsureDockerIsReady();
            return Task.CompletedTask;
        }

        public static string RunDockerCommand(string arguments, int timeoutMs = 10000)
        {
            using var process = new Process
            {
                StartInfo = CreateDockerProcessStartInfo(arguments)
            };

            if (!process.Start())
            {
                throw new InvalidOperationException($"Failed to start docker {arguments}.");
            }

            if (!process.WaitForExit(timeoutMs))
            {
                try
                {
                    process.Kill(true);
                }
                catch
                {
                    // Best-effort cleanup.
                }

                throw new TimeoutException($"docker {arguments} timed out.");
            }

            var output = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd().Trim();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(stderr)
                        ? $"docker {arguments} exited with code {process.ExitCode}."
                        : stderr);
            }

            return output;
        }

        private static bool TryGetServerVersion(out string? error)
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = CreateDockerProcessStartInfo("version --format {{.Server.Version}}")
                };

                if (!process.Start())
                {
                    error = "Failed to start docker process.";
                    return false;
                }

                if (!process.WaitForExit(5000))
                {
                    try
                    {
                        process.Kill(true);
                    }
                    catch
                    {
                        // Best-effort cleanup.
                    }

                    error = "docker version timed out.";
                    return false;
                }

                var output = process.StandardOutput.ReadToEnd().Trim();
                var stderr = process.StandardError.ReadToEnd().Trim();

                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    error = null;
                    return true;
                }

                error = string.IsNullOrWhiteSpace(stderr)
                    ? $"docker version exited with code {process.ExitCode}."
                    : stderr;

                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static string? WaitForDocker(TimeSpan timeout, TimeSpan pollInterval)
        {
            var deadline = DateTime.UtcNow.Add(timeout);
            string? lastError = null;

            while (DateTime.UtcNow < deadline)
            {
                if (TryGetServerVersion(out lastError))
                {
                    return null;
                }

                Thread.Sleep(pollInterval);
            }

            return lastError ?? "unknown error";
        }

        private static ProcessStartInfo CreateDockerProcessStartInfo(string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            startInfo.Environment.Remove("DOCKER_HOST");
            startInfo.Environment.Remove("DOCKER_CONTEXT");
            ConfigureDockerCliEnvironment(startInfo.Environment);

            return startInfo;
        }

        private static void ConfigureDockerCliEnvironment(IDictionary<string, string?> environment)
        {
            Directory.CreateDirectory(DockerCliConfigDirectory);

            var configPath = Path.Combine(DockerCliConfigDirectory, "config.json");
            if (!File.Exists(configPath))
            {
                File.WriteAllText(configPath, "{}");
            }

            environment["DOCKER_CONFIG"] = DockerCliConfigDirectory;
        }
    }
}
