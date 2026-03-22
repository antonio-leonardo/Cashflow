using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Infrastructure.Test
{
    public sealed class KeycloakContainerFixture : IAsyncLifetime
    {
        private const string RealmName = "cashflow";
        private const string ClientId = "cashflow-api";
        private const string Username = "integration-user";
        private const string Password = "P@ssw0rd!";
        private const string ClientSecret = "Teste";

        private readonly IContainer _container;

        public KeycloakContainerFixture()
        {
            var realmImportPath = GetRealmImportPath();

            _container = new ContainerBuilder("quay.io/keycloak/keycloak:24.0.5")
                .WithPortBinding(8080, true)
                .WithEnvironment("KEYCLOAK_ADMIN", "admin")
                .WithEnvironment("KEYCLOAK_ADMIN_PASSWORD", "admin")
                .WithEnvironment("KC_HTTP_ENABLED", "true")
                .WithEnvironment("KC_HOSTNAME_STRICT", "false")
                .WithEnvironment("KC_HOSTNAME_STRICT_HTTPS", "false")
                .WithEnvironment("KC_LOG_LEVEL", "DEBUG")
                .WithBindMount(realmImportPath, "/opt/keycloak/data/import/realm-cashflow.json")
                .WithCommand("start-dev", "--import-realm", "--http-enabled=true")
                .WithOutputConsumer(Consume.RedirectStdoutAndStderrToConsole())
                .Build();
        }

        public string Authority => $"{BaseAddress}realms/{RealmName}";

        public Uri BaseAddress =>
            new($"http://{_container.Hostname}:{_container.GetMappedPublicPort(8080)}");

        public async Task InitializeAsync()
        {
            await _container.StartAsync();
            await WaitUntilReadyAsync();
        }

        public async Task DisposeAsync()
        {
            await _container.DisposeAsync();
        }

        public async Task<string> GetAccessTokenClientIdSecretAsync(string scope = null)
        {
            using var client = new HttpClient();
            var tokenEndpoint = $"{Authority}/protocol/openid-connect/token";
            var form = new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["grant_type"] = "client_credentials"
            };
            if(!string.IsNullOrWhiteSpace(scope)) form["scope"] = scope;
            var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
            {
                Content = new FormUrlEncodedContent(form)
            };

            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
            return payload.GetProperty("access_token").GetString()!;
        }

        private static string GetRealmImportPath()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "keycloak", "realm-cashflow.json");

            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "Arquivo de importacao do realm do Keycloak nao encontrado.",
                    path);
            }

            return Path.GetFullPath(path);
        }

        private async Task WaitUntilReadyAsync()
        {
            using var client = new HttpClient { BaseAddress = BaseAddress };
            var deadline = DateTime.UtcNow.AddMinutes(2);

            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    var response = await client.GetAsync($"/realms/{RealmName}");
                    if (response.IsSuccessStatusCode)
                    {
                        return;
                    }
                }
                catch (HttpRequestException)
                {
                    // The container might still be booting.
                }

                await Task.Delay(TimeSpan.FromSeconds(2));
            }

            throw new TimeoutException("Keycloak nao ficou pronto dentro do tempo esperado.");
        }

        private sealed record TokenResponse(
            [property: JsonPropertyName("access_token")] string AccessToken);
    }
}

