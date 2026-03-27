using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Infrastructure.Test
{
    public sealed class KeycloakContainerFixture : IAsyncLifetime
    {
        private const string RealmName = "cashflow";
        private const string AdminRealmName = "master";
        private const string AdminUsername = "admin";
        private const string AdminPassword = "admin";
        private const string AdminClientId = "admin-cli";
        private const string WriterClientId = "cashflow-api";
        private const string ReadOnlyClientId = "cashflow-readonly";

        private readonly IContainer _container;
        private string _writerClientSecret = string.Empty;
        private string _readOnlyClientSecret = string.Empty;

        public KeycloakContainerFixture()
        {
            var realmImportPath = GetRealmImportPath();

            _container = new ContainerBuilder("quay.io/keycloak/keycloak:24.0.5")
                .WithPortBinding(8080, true)
                .WithEnvironment("KEYCLOAK_ADMIN", AdminUsername)
                .WithEnvironment("KEYCLOAK_ADMIN_PASSWORD", AdminPassword)
                .WithEnvironment("KC_HTTP_ENABLED", "true")
                .WithEnvironment("KC_HOSTNAME_STRICT", "false")
                .WithEnvironment("KC_HOSTNAME_STRICT_HTTPS", "false")
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
            await ResolveClientSecretsAsync();
        }

        public async Task DisposeAsync()
        {
            await _container.DisposeAsync();
        }

        public Task<string> GetAccessTokenClientIdSecretAsync(string? scope = null)
            => GetAccessTokenAsync(WriterClientId, _writerClientSecret, scope);

        public Task<string> GetReadOnlyAccessTokenClientIdSecretAsync(string? scope = null)
            => GetAccessTokenAsync(ReadOnlyClientId, _readOnlyClientSecret, scope);

        public async Task<string> GetAccessTokenAsync(string clientId, string clientSecret, string? scope = null)
        {
            if (string.IsNullOrWhiteSpace(clientSecret))
            {
                throw new InvalidOperationException(
                    $"Client secret for '{clientId}' is not initialized.");
            }

            using var client = new HttpClient();
            var tokenEndpoint = $"{Authority}/protocol/openid-connect/token";
            var form = new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["grant_type"] = "client_credentials"
            };

            if (!string.IsNullOrWhiteSpace(scope))
            {
                form["scope"] = scope;
            }

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

        private async Task ResolveClientSecretsAsync()
        {
            var adminAccessToken = await GetAdminAccessTokenAsync();
            _writerClientSecret = await GetClientSecretAsync(WriterClientId, adminAccessToken);
            _readOnlyClientSecret = await GetClientSecretAsync(ReadOnlyClientId, adminAccessToken);
        }

        private async Task<string> GetAdminAccessTokenAsync()
        {
            using var client = new HttpClient { BaseAddress = BaseAddress };
            var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"/realms/{AdminRealmName}/protocol/openid-connect/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = AdminClientId,
                    ["username"] = AdminUsername,
                    ["password"] = AdminPassword,
                    ["grant_type"] = "password"
                })
            };

            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
            return payload.GetProperty("access_token").GetString()!;
        }

        private async Task<string> GetClientSecretAsync(string clientId, string adminAccessToken)
        {
            var internalClientId = await GetClientInternalIdAsync(clientId, adminAccessToken);

            using var client = new HttpClient { BaseAddress = BaseAddress };
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", adminAccessToken);

            var response = await client.GetAsync($"/admin/realms/{RealmName}/clients/{internalClientId}/client-secret");
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
            var secret = payload.GetProperty("value").GetString();

            if (string.IsNullOrWhiteSpace(secret))
            {
                throw new InvalidOperationException(
                    $"Could not resolve secret for client '{clientId}'.");
            }

            return secret;
        }

        private async Task<string> GetClientInternalIdAsync(string clientId, string adminAccessToken)
        {
            using var client = new HttpClient { BaseAddress = BaseAddress };
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", adminAccessToken);

            var response = await client.GetAsync($"/admin/realms/{RealmName}/clients?clientId={Uri.EscapeDataString(clientId)}");
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (payload.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException(
                    $"Unexpected payload while resolving client id '{clientId}'.");
            }

            var iterator = payload.EnumerateArray();
            if (!iterator.MoveNext())
            {
                throw new InvalidOperationException(
                    $"Client '{clientId}' not found in realm '{RealmName}'.");
            }

            var internalClientId = iterator.Current.GetProperty("id").GetString();
            if (string.IsNullOrWhiteSpace(internalClientId))
            {
                throw new InvalidOperationException(
                    $"Client '{clientId}' has no internal id.");
            }

            return internalClientId;
        }
    }
}
