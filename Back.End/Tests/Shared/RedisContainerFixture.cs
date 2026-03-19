using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Infrastructure.Test
{
    public class RedisContainerFixture : IAsyncLifetime
    {
        private readonly IContainer _container;

        public RedisContainerFixture()
        {
            _container = new ContainerBuilder("redis:7")
                .WithPortBinding(6379, true)
                .Build();
        }

        public string ConnectionString =>
            $"localhost:{_container.GetMappedPublicPort(6379)}";

        public async Task InitializeAsync()
        {
            await _container.StartAsync();
        }

        public async Task DisposeAsync()
        {
            await _container.DisposeAsync();
        }
    }
}