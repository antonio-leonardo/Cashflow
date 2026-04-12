using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Infrastructure.Test
{
    /// <summary>
    /// Spins up the Azure Cosmos DB Emulator in Docker with the MongoDB API enabled.
    ///
    /// Image: mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:mongodb
    /// Reference: https://learn.microsoft.com/azure/cosmos-db/emulator
    ///
    /// The emulator exposes the MongoDB wire protocol on port 10255 (TLS) and
    /// the management port on 8081. For integration tests we disable TLS using
    /// the connection string parameter "?ssl=false&retrywrites=false".
    ///
    /// NOTE: This container is resource-intensive (~2 GB RAM). Tests that use
    /// this fixture must be tagged [Trait("Category","CosmosEmulator")] so they
    /// can be skipped on resource-constrained agents:
    ///   dotnet test --filter "Category!=CosmosEmulator"
    /// </summary>
    public class CosmosDbEmulatorFixture : IAsyncLifetime
    {
        private const string EmulatorImage = "mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:mongodb";
        private const int    MongoPort     = 10255;

        private IContainer? _container;

        public string MongoConnectionString { get; private set; } = string.Empty;

        public async Task InitializeAsync()
        {
            _container = new ContainerBuilder(EmulatorImage)
                .WithEnvironment("AZURE_COSMOS_EMULATOR_PARTITION_COUNT", "1")
                .WithEnvironment("AZURE_COSMOS_EMULATOR_ENABLE_DATA_PERSISTENCE", "false")
                .WithPortBinding(MongoPort, true)
                .WithPortBinding(8081,      true)
                .WithWaitStrategy(Wait.ForUnixContainer()
                    .UntilInternalTcpPortIsAvailable(MongoPort)
                    .UntilHttpRequestIsSucceeded(req => req
                        .ForPort(8081)
                        .ForPath("/_explorer/index.html")))
                .Build();

            await _container.StartAsync();

            var port = _container.GetMappedPublicPort(MongoPort);

            // The Cosmos DB Emulator uses a fixed primary key.
            const string primaryKey =
                "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";

            MongoConnectionString =
                $"mongodb://localhost:{primaryKey}@127.0.0.1:{port}" +
                $"/?ssl=false&retrywrites=false&maxIdleTimeMS=120000";
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
