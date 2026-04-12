using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Infrastructure.Test
{
    /// <summary>
    /// Spins up the Azurite Azure Storage Emulator in Docker.
    /// Provides Blob Service endpoints for integration tests.
    ///
    /// Image: mcr.microsoft.com/azure-storage/azurite
    /// Ports:  10000 (Blob), 10001 (Queue), 10002 (Table)
    ///
    /// The well-known development connection string is the same used by the
    /// Azure Storage SDK against Azurite:
    ///   DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;
    ///   AccountKey=...;BlobEndpoint=http://127.0.0.1:{port}/devstoreaccount1;
    /// </summary>
    public class AzuriteContainerFixture : IAsyncLifetime
    {
        // Azurite uses a fixed well-known development account
        private const string AzuriteAccountName = "devstoreaccount1";
        private const string AzuriteAccountKey  =
            "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";

        private IContainer? _container;

        public int BlobPort { get; private set; }

        public string BlobConnectionString =>
            $"DefaultEndpointsProtocol=http;" +
            $"AccountName={AzuriteAccountName};" +
            $"AccountKey={AzuriteAccountKey};" +
            $"BlobEndpoint=http://127.0.0.1:{BlobPort}/{AzuriteAccountName};";

        public async Task InitializeAsync()
        {
            _container = new ContainerBuilder("mcr.microsoft.com/azure-storage/azurite:latest")
                .WithCommand("azurite-blob", "--blobHost", "0.0.0.0", "--loose")
                .WithPortBinding(10000, true)
                .WithWaitStrategy(Wait.ForUnixContainer()
                    .UntilInternalTcpPortIsAvailable(10000))
                .Build();

            await _container.StartAsync();
            BlobPort = _container.GetMappedPublicPort(10000);
        }

        public async Task DisposeAsync()
        {
            if (_container is not null)
            {
                await _container.DisposeAsync();
            }
        }
    }
}
