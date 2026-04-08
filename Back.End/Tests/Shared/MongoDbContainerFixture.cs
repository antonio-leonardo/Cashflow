using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;

namespace Infrastructure.Test
{
    public class MongoDbContainerFixture : IAsyncLifetime
    {
        private const string MongoImage = "mongo:7.0.31";
        private readonly string _alias;
        private readonly IContainer _container;

        public MongoDbContainerFixture(INetwork network, string alias)
        {
            _alias = alias;
            _container = new ContainerBuilder(MongoImage)
                .WithPortBinding(27017, true)
                .WithNetwork(network)
                .WithNetworkAliases(alias)
                .WithCommand("--bind_ip_all")
                .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(27017))
                .WithOutputConsumer(Consume.RedirectStdoutAndStderrToConsole())
                .Build();
        }

        public async Task InitializeAsync() => await _container.StartAsync();
        public async Task DisposeAsync() => await _container.DisposeAsync();

        public string ConnectionString => $"mongodb://127.0.0.1:{_container.GetMappedPublicPort(27017)}";

        public string NetworkConnectionString => $"mongodb://{_alias}:27017";
    }
}
