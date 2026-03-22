using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Infrastructure.Test
{
    public class TransactionApiContainerFixture : IAsyncLifetime
    {
        private readonly HolisticCompleteInfrastructureFixture _infra;
        private IContainer? _container;

        public Uri BaseAddress { get; private set; } = default!;

        private const string IMAGE_NAME = "cashflow-transaction-api:latest";

        public TransactionApiContainerFixture(HolisticCompleteInfrastructureFixture infra)
        {
            _infra = infra;
        }

        public async Task InitializeAsync()
        {
            var image = new ImageFromDockerfileBuilder()
                .WithDockerfileDirectory(CommonDirectoryPath.GetSolutionDirectory(), string.Empty)
                .WithDockerfile("Back.End/Service/Transaction/API/Dockerfile")
                .WithName(IMAGE_NAME)
                .WithDeleteIfExists(true)
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
            .WithWaitStrategy(Wait.ForUnixContainer()
                    .UntilInternalTcpPortIsAvailable(8080)
                    .UntilHttpRequestIsSucceeded(r => r
                        .ForPath("/api/transactions")
                        .ForPort(8080)
                        .ForStatusCode(System.Net.HttpStatusCode.MethodNotAllowed)))
            .Build();

            await _container.StartAsync();

            var port = _container.GetMappedPublicPort(8080);
            BaseAddress = new Uri($"http://127.0.0.1:{port}");
        }

        public async Task DisposeAsync()
        {
            if (_container is not null)
                await _container.DisposeAsync();
        }
    }
}
