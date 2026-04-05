using System.Runtime.CompilerServices;

namespace Infrastructure.Test
{
    internal static class DockerHostBootstrap
    {
        private const string DockerDotNetDockerEnginePipeUri = "npipe://./pipe/docker_engine";

        [ModuleInitializer]
        internal static void Initialize()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            var dockerHost = Environment.GetEnvironmentVariable("DOCKER_HOST");

            if (ShouldPreserveDockerHost(dockerHost))
            {
                return;
            }

            Environment.SetEnvironmentVariable("DOCKER_HOST", DockerDotNetDockerEnginePipeUri);
        }

        private static bool ShouldPreserveDockerHost(string? dockerHost)
        {
            return !string.IsNullOrWhiteSpace(dockerHost)
                && !dockerHost.Contains("docker_engine", StringComparison.OrdinalIgnoreCase)
                && !dockerHost.Contains("dockerDesktopLinuxEngine", StringComparison.OrdinalIgnoreCase);
        }
    }
}
