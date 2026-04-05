using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using System.Net;

namespace Infrastructure.Test
{
    public class TransactionApiContainerFixture : IAsyncLifetime
    {
        private const string ImageNamePrefix = "cashflow-transaction-api";
        private readonly HolisticCompleteInfrastructureFixture _infra;
        private readonly string _imageName = $"{ImageNamePrefix}:test-{Guid.NewGuid():N}";
        private IContainer? _container;

        public Uri BaseAddress { get; private set; } = default!;

        public TransactionApiContainerFixture(HolisticCompleteInfrastructureFixture infra)
        {
            _infra = infra;
        }

        public async Task InitializeAsync()
        {
            var image = new ImageFromDockerfileBuilder()
                .WithDockerfileDirectory(CommonDirectoryPath.GetSolutionDirectory(), "Back.End")
                .WithDockerfile("Service/Transaction/API/Dockerfile")
                .WithName(_imageName)
                .Build();

            await image.CreateAsync();

            _container = new ContainerBuilder(image)
            .WithImage(image)
            .WithPortBinding(0, 8080)
            .WithNetwork(_infra.Network)
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Testing")
            .WithEnvironment("ASPNETCORE_URLS", "http://+:8080")
            .WithEnvironment("ConnectionStrings__Postgres",
                _infra.PostgresContainerFixture.NetworkConnectionString)
            .WithEnvironment("RabbitMq__Host",
                _infra.RabbitMqContainerFixture.NetworkHost)
            .WithEnvironment("RabbitMq__Port",
                _infra.RabbitMqContainerFixture.NetworkPort.ToString())
            .WithEnvironment("RabbitMq__Username", "guest")
            .WithEnvironment("RabbitMq__Password", "guest")
            .WithEnvironment("Mongo__Connection",
                _infra.MongoDbContainerFixture.NetworkConnectionString)
            .WithEnvironment("Redis__Connection",
                _infra.RedisContainerFixture.NetworkConnectionString)
            .Build();

            await _container.StartAsync();

            var port = _container.GetMappedPublicPort(8080);
            BaseAddress = new Uri($"http://127.0.0.1:{port}");
            await WaitUntilReadyAsync();
        }

        public async Task DisposeAsync()
        {
            if (_container is not null)
                await _container.DisposeAsync();
        }

        private async Task WaitUntilReadyAsync()
        {
            using var client = new HttpClient { BaseAddress = BaseAddress };
            var deadline = DateTime.UtcNow.AddMinutes(2);

            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    // /api/transactions without method payload usually returns 405 when endpoint is up.
                    var response = await client.GetAsync("/api/transactions");
                    if (response.StatusCode == HttpStatusCode.MethodNotAllowed ||
                        response.IsSuccessStatusCode)
                    {
                        return;
                    }
                }
                catch (HttpRequestException)
                {
                    // The API container is still starting.
                }

                await Task.Delay(TimeSpan.FromSeconds(2));
            }

            throw new TimeoutException("Transaction API nao ficou pronta dentro do tempo esperado.");
        }
    }
}

