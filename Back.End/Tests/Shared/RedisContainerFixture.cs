using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;

namespace Infrastructure.Test
{
    public class RedisContainerFixture : IAsyncLifetime
    {
        private readonly string _alias;
        private readonly IContainer _container;

        public RedisContainerFixture(INetwork network, string alias)
        {
            _alias = alias;
            _container = new ContainerBuilder("redis:7")
                .WithPortBinding(6379, true)
                .WithNetwork(network)
                .WithNetworkAliases(alias)
                .Build();
        }

        public async Task InitializeAsync() => await _container.StartAsync();
        public async Task DisposeAsync() => await _container.DisposeAsync();

        public string ConnectionString =>
            $"localhost:{_container.GetMappedPublicPort(6379)}";

        public string NetworkConnectionString => $"{_alias}:6379";
    }
}