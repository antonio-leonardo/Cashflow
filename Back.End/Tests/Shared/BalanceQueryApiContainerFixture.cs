using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using System.Net;

namespace Infrastructure.Test
{
    public class BalanceQueryApiContainerFixture : IAsyncLifetime
    {
        private const string ImageNamePrefix = "cashflow-balance-query-api";
        private readonly BaseCompleteInfrastructureFixture _infra;
        private readonly string _keycloakAuthority;
        private readonly string _imageName = $"{ImageNamePrefix}:test-{Guid.NewGuid():N}";
        private IContainer? _container;

        public Uri BaseAddress { get; private set; } = default!;

        public BalanceQueryApiContainerFixture(BaseCompleteInfrastructureFixture infra, string? keycloakAuthority = null)
        {
            _infra = infra;
            _keycloakAuthority = keycloakAuthority ?? "http://keycloak:8080/realms/cashflow";
        }

        public BalanceQueryApiContainerFixture(HolisticCompleteInfrastructureFixture infra)
            : this((BaseCompleteInfrastructureFixture)infra, infra.KeycloakFixture.Authority)
        {
        }

        public async Task InitializeAsync()
        {
            var image = new ImageFromDockerfileBuilder()
                .WithDockerfileDirectory(CommonDirectoryPath.GetSolutionDirectory(), "Back.End")
                .WithDockerfile("Service/Balance/API/Dockerfile")
                .WithName(_imageName)
                .Build();

            await image.CreateAsync();

            _container = new ContainerBuilder(image)
                .WithImage(image)
                .WithPortBinding(0, 8080)
                .WithNetwork(_infra.Network)
                .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Testing")
                .WithEnvironment("ASPNETCORE_URLS", "http://+:8080")
                .WithEnvironment("Redis__Connection",
                    _infra.RedisContainerFixture.NetworkConnectionString)
                .WithEnvironment("Mongo__Connection",
                    _infra.MongoDbContainerFixture.NetworkConnectionString)
                .WithEnvironment("LocalStorage__BasePath", "/tmp/cashflow-reports")
                .WithEnvironment("Keycloak__Authority", _keycloakAuthority)
                .WithEnvironment("Keycloak__Audience", "cashflow-api")
                .Build();

            await _container.StartAsync();

            var port = _container.GetMappedPublicPort(8080);
            BaseAddress = new Uri($"http://127.0.0.1:{port}");

            await WaitUntilReadyAsync();
        }

        public async Task DisposeAsync()
        {
            if (_container is not null)
            {
                await _container.DisposeAsync();
            }
        }

        private async Task WaitUntilReadyAsync()
        {
            using var client = new HttpClient
            {
                BaseAddress = BaseAddress
            };

            var deadline = DateTime.UtcNow.AddMinutes(2);

            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    var response = await client.GetAsync("/health/ready");
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        return;
                    }
                }
                catch (HttpRequestException)
                {
                    // API ainda iniciando.
                }

                await Task.Delay(TimeSpan.FromSeconds(2));
            }

            throw new TimeoutException("Balance Query API nao ficou pronta dentro do tempo esperado.");
        }
    }
}
