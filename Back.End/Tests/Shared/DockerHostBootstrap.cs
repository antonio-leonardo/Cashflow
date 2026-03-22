using System.Runtime.CompilerServices;

namespace Infrastructure.Test
{
    internal static class DockerHostBootstrap
    {
        private const string InvalidDockerDesktopLinuxEngine = "npipe:////pipe/dockerDesktopLinuxEngine";
        private const string DockerEnginePipeUri = "npipe://./pipe/docker_engine";

        [ModuleInitializer]
        internal static void Initialize()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            var dockerHost = Environment.GetEnvironmentVariable("DOCKER_HOST");

            if (string.IsNullOrWhiteSpace(dockerHost) ||
                string.Equals(dockerHost, InvalidDockerDesktopLinuxEngine, StringComparison.OrdinalIgnoreCase))
            {
                Environment.SetEnvironmentVariable("DOCKER_HOST", DockerEnginePipeUri);
            }
        }
    }
}