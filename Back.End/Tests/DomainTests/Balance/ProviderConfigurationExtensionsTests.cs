using Cashflow.Shared.Contracts.Configuration;
using Cashflow.Shared.Identity.Abstractions;
using Cashflow.Shared.Secrets.Abstractions;
using Microsoft.Extensions.Configuration;

namespace Balance.Domain.Tests;

public class ProviderConfigurationExtensionsTests
{
    [Fact]
    public void GetConfiguredProvider_ShouldReturnDefault_WhenValueIsMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var provider = configuration.GetConfiguredProvider(
            "Providers:Identity",
            IdentityProvider.Keycloak);

        Assert.Equal(IdentityProvider.Keycloak, provider);
    }

    [Fact]
    public void GetConfiguredProvider_ShouldParseValues_CaseInsensitively()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Providers:Secrets"] = "azurekeyvault"
            })
            .Build();

        var provider = configuration.GetConfiguredProvider(
            "Providers:Secrets",
            SecretsProvider.Local);

        Assert.Equal(SecretsProvider.AzureKeyVault, provider);
    }

    [Fact]
    public void GetConfiguredProvider_ShouldThrow_WhenValueIsUnsupported()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Providers:Identity"] = "Auth0"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            configuration.GetConfiguredProvider(
                "Providers:Identity",
                IdentityProvider.Keycloak));

        Assert.Contains("Unsupported provider 'Auth0'", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
