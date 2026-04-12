using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Networks;

namespace Infrastructure.Test
{
    /// <summary>
    /// Spins up the official Microsoft Service Bus Emulator alongside SQL Server Express
    /// (required for emulator persistence) using a shared Docker network.
    ///
    /// Connection string format (with UseDevelopmentEmulator=true):
    ///   Endpoint=sb://127.0.0.1;SharedAccessKeyName=RootManageSharedAccessKey;
    ///   SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;
    ///
    /// Topics/subscriptions are provisioned via the Config.json file mounted into the emulator.
    /// Defaults defined here: topic "transactioncreatedeventv1" with subscriptions
    /// consumer-a, consumer-b and dlq-test (MaxDeliveryCount=2).
    ///
    /// References:
    ///   https://learn.microsoft.com/azure/service-bus-messaging/test-locally-with-service-bus-emulator
    /// </summary>
    public class ServiceBusEmulatorFixture : IAsyncLifetime
    {
        // SA password must satisfy SQL Server complexity rules
        private const string SqlSaPassword = "Cashflow@Emul4tor!";
        private const string SqlAlias      = "mssql";
        private const string EmulatorAlias = "servicebus-emulator";
        private const int    EmulatorAmqpPort = 5672;
        private const string FixtureLabelKey = "cashflow.test.fixture";
        private const string FixtureLabelValue = "servicebus-emulator";
        private const string EmulatorImage = "mcr.microsoft.com/azure-messaging/servicebus-emulator:latest";

        // Default SAS key used by the emulator — keep in sync with Config.json
        private const string EmulatorSasKey = "SAS_KEY_VALUE";

        private INetwork?   _network;
        private IContainer? _sqlContainer;
        private IContainer? _emulatorContainer;
        private string?     _configFilePath;

        public string ConnectionString { get; private set; } = string.Empty;
        public Uri? CustomEndpointAddress { get; private set; }

        public async Task InitializeAsync()
        {
            try
            {
                CleanupStaleEmulatorContainers();

                _network = new NetworkBuilder().Build();
                await _network.CreateAsync();

                // 1. SQL Server Express (required by the Service Bus Emulator)
                _sqlContainer = new ContainerBuilder("mcr.microsoft.com/mssql/server:2022-latest")
                    .WithNetwork(_network)
                    .WithNetworkAliases(SqlAlias)
                    .WithEnvironment("ACCEPT_EULA", "Y")
                    .WithEnvironment("SA_PASSWORD", SqlSaPassword)
                    .WithLabel(FixtureLabelKey, FixtureLabelValue)
                    .WithPortBinding(1433, true)
                    .WithWaitStrategy(Wait.ForUnixContainer()
                        .UntilInternalTcpPortIsAvailable(1433))
                    .Build();

                await _sqlContainer.StartAsync();

                // 2. Config.json for emulator topology (written to a temp file and mounted)
                _configFilePath = WriteEmulatorConfig();

                // 3. Service Bus Emulator
                _emulatorContainer = new ContainerBuilder("mcr.microsoft.com/azure-messaging/servicebus-emulator:latest")
                    .WithNetwork(_network)
                    .WithNetworkAliases(EmulatorAlias)
                    .WithEnvironment("ACCEPT_EULA", "Y")
                    .WithEnvironment("MSSQL_SA_PASSWORD", SqlSaPassword)
                    .WithEnvironment("SQL_SERVER", SqlAlias)
                    .WithLabel(FixtureLabelKey, FixtureLabelValue)
                    .WithBindMount(_configFilePath, "/ServiceBus_Emulator/ConfigFiles/Config.json", AccessMode.ReadOnly)
                    .WithPortBinding(EmulatorAmqpPort, true)
                    .WithWaitStrategy(Wait.ForUnixContainer()
                        .UntilMessageIsLogged("Emulator Service is Successfully Up"))
                    .Build();

                await _emulatorContainer.StartAsync();

                var mappedPort = _emulatorContainer.GetMappedPublicPort(EmulatorAmqpPort);
                CustomEndpointAddress = new Uri($"sb://127.0.0.1:{mappedPort}");
                ConnectionString =
                    $"Endpoint=sb://localhost;" +
                    $"SharedAccessKeyName=RootManageSharedAccessKey;" +
                    $"SharedAccessKey={EmulatorSasKey};" +
                    $"UseDevelopmentEmulator=true;";
            }
            catch
            {
                await DisposeAsync();
                throw;
            }
        }

        public async Task DisposeAsync()
        {
            if (_emulatorContainer is not null) await _emulatorContainer.DisposeAsync();
            if (_sqlContainer      is not null) await _sqlContainer.DisposeAsync();
            if (_network           is not null) await _network.DeleteAsync();

            if (_configFilePath is not null && File.Exists(_configFilePath))
            {
                File.Delete(_configFilePath);
            }
        }

        private static void CleanupStaleEmulatorContainers()
        {
            DockerTestEnvironment.EnsureDockerIsReady();

            // Clean up prior emulator containers created by this fixture so a previous failed run
            // does not keep port 5672 occupied for the next execution.
            RemoveContainersByQuery($"ps -aq --filter \"label={FixtureLabelKey}={FixtureLabelValue}\"");

            // Backward-compatible cleanup for containers created before labels were introduced.
            RemoveContainersByQuery(
                $"ps -aq --filter \"ancestor={EmulatorImage}\" --filter \"publish={EmulatorAmqpPort}\"");
        }

        private static void RemoveContainersByQuery(string queryArguments)
        {
            var output = DockerTestEnvironment.RunDockerCommand(queryArguments);
            var containerIds = output
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (containerIds.Length == 0)
            {
                return;
            }

            DockerTestEnvironment.RunDockerCommand($"rm -f {string.Join(' ', containerIds)}", 20000);
        }

        /// <summary>
        /// Writes the emulator Config.json with the test topology and returns the file path.
        /// Topic "transactioncreatedeventv1" with subscriptions required by integration tests.
        /// </summary>
        private static string WriteEmulatorConfig()
        {
            const string config = """
            {
              "UserConfig": {
                "Namespaces": [
                  {
                    "Name": "sbemulatorns",
                    "Queues": [],
                    "Topics": [
                      {
                        "Name": "transactioncreatedeventv1",
                        "Properties": {
                          "MaxDeliveryCount": 3,
                          "DefaultMessageTimeToLive": "PT10M"
                        },
                        "Subscriptions": [
                          { "Name": "consumer-a", "Properties": {} },
                          { "Name": "consumer-b", "Properties": {} },
                          {
                            "Name": "session-test",
                            "Properties": { "RequiresSession": true, "MaxDeliveryCount": 3 }
                          },
                          {
                            "Name": "dlq-test",
                            "Properties": { "MaxDeliveryCount": 2 }
                          }
                        ]
                      }
                    ]
                  }
                ],
                "Logging": { "Type": "Console" }
              }
            }
            """;

            var path = Path.Combine(Path.GetTempPath(), $"sbemulator-{Guid.NewGuid():N}.json");
            File.WriteAllText(path, config);
            return path;
        }
    }
}
