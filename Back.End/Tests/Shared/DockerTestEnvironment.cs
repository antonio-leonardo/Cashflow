using System.Diagnostics;

namespace Infrastructure.Test
{
    internal static class DockerTestEnvironment
    {
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

        private static bool TryGetServerVersion(out string? error)
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "docker",
                        Arguments = "version --format {{.Server.Version}}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.StartInfo.Environment.Remove("DOCKER_HOST");
                process.StartInfo.Environment.Remove("DOCKER_CONTEXT");

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
    }
}
