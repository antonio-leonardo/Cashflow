using Cashflow.Gateway;
using Cashflow.Shared.Infrastructure.DependencyInjection;
using Cashflow.Shared.Messaging.Abstractions;
using Cashflow.Shared.Messaging.AzureServiceBus.MessageBus;
using Cashflow.Shared.Messaging.RabbitMQ.MessageBus;
using Cashflow.Shared.NoSql.MongoDB;
using Cashflow.Shared.NoSql.Redis;
using Cashflow.Shared.Observability;
using Cashflow.Shared.Secrets.Abstractions;
using Cashflow.Shared.Secrets.AzureKeyVault;
using Cashflow.Shared.Storage.Abstractions;
using Cashflow.Shared.Storage.AzureBlob;
using Cashflow.Shared.Storage.Local;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Balance.Domain.Tests;

public class MulticloudProviderConfigurationTests
{
    [Fact]
    public void AddCashflowMessaging_ShouldRegisterRabbitMqBus_WhenProviderIsDefault()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>());

        services.AddCashflowMessaging(configuration);

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IMessageBus) &&
            descriptor.ImplementationType == typeof(RabbitMqBus));
    }

    [Fact]
    public void AddCashflowMessaging_ShouldRegisterAzureServiceBusBus_WhenProviderIsConfigured()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Providers:Messaging"] = "AzureServiceBus",
            ["AzureServiceBus:ConnectionString"] = "Endpoint=sb://cashflow.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test"
        });

        services.AddCashflowMessaging(configuration);

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IMessageBus) &&
            descriptor.ImplementationType == typeof(AzureServiceBusBus));
    }

    [Fact]
    public void AddCashflowMessaging_ShouldAcceptAzureServiceBusManagedIdentity_WhenNamespaceIsConfigured()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Providers:Messaging"] = "AzureServiceBus",
            ["AzureServiceBus:UseManagedIdentity"] = "true",
            ["AzureServiceBus:Namespace"] = "cashflow.servicebus.windows.net"
        });

        services.AddCashflowMessaging(configuration);

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IMessageBus) &&
            descriptor.ImplementationType == typeof(AzureServiceBusBus));
    }

    [Fact]
    public void AddCashflowMessaging_ShouldThrow_WhenAzureServiceBusManagedIdentityNamespaceIsMissing()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Providers:Messaging"] = "AzureServiceBus",
            ["AzureServiceBus:UseManagedIdentity"] = "true"
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddCashflowMessaging(configuration));

        Assert.Contains("AzureServiceBus:Namespace", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddCashflowMessaging_ShouldThrow_WhenProviderIsUnsupported()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Providers:Messaging"] = "Kafka"
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddCashflowMessaging(configuration));

        Assert.Contains("Unsupported provider 'Kafka'", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddCashflowIdentity_ShouldThrow_WhenEntraIdConfigurationIsMissing()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Providers:Identity"] = "EntraId"
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddCashflowIdentity(configuration));

        Assert.Contains("EntraId:TenantId", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddCashflowIdentity_ShouldAcceptKeycloak_WhenDefaultConfigurationIsComplete()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Keycloak:Authority"] = "https://keycloak",
            ["Keycloak:Audience"] = "cashflow-api"
        });

        services.AddCashflowIdentity(configuration);

        Assert.NotEmpty(services);
    }

    [Fact]
    public void AddCashflowIdentity_ShouldAcceptEntraId_WhenConfigurationIsComplete()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Providers:Identity"] = "EntraId",
            ["EntraId:TenantId"] = "tenant-id",
            ["EntraId:ClientId"] = "client-id",
            ["EntraId:Audience"] = "api://cashflow-api"
        });

        services.AddCashflowIdentity(configuration);

        Assert.NotEmpty(services);
    }

    [Fact]
    public void AddCashflowIdentity_ShouldThrow_WhenProviderIsUnsupported()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Providers:Identity"] = "Auth0"
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddCashflowIdentity(configuration));

        Assert.Contains("Unsupported provider 'Auth0'", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddCashflowSecrets_ShouldRegisterLocalSecretResolver_WhenProviderIsDefault()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfigurationManager(new Dictionary<string, string?>());

        services.AddCashflowSecrets(configuration);

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ISecretResolver) &&
            descriptor.ImplementationType?.Name == "LocalConfigurationSecretResolver");
    }

    [Fact]
    public void AddAzureKeyVaultSecretResolver_ShouldRegisterAzureKeyVaultSecretResolver_WhenVaultUriIsConfigured()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfigurationManager(new Dictionary<string, string?>
        {
            ["AzureKeyVault:VaultUri"] = "https://cashflow-kv.vault.azure.net/"
        });

        services.AddAzureKeyVaultSecretResolver(configuration);

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ISecretResolver) &&
            descriptor.ImplementationType == typeof(AzureKeyVaultSecretResolver));
    }

    [Fact]
    public async Task AddCashflowSecrets_ShouldResolveLocalSecrets_FromConfiguration()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfigurationManager(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = "Host=postgres"
        });

        services.AddSingleton<IConfiguration>(configuration);
        services.AddCashflowSecrets(configuration);

        await using var provider = services.BuildServiceProvider().CreateAsyncScope();
        var resolver = provider.ServiceProvider.GetRequiredService<ISecretResolver>();

        var value = await resolver.GetSecretAsync("ConnectionStrings:Postgres");

        Assert.Equal("Host=postgres", value);
    }

    [Fact]
    public void AddCashflowSecrets_ShouldThrow_WhenProviderIsUnsupported()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfigurationManager(new Dictionary<string, string?>
        {
            ["Providers:Secrets"] = "HashiVault"
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddCashflowSecrets(configuration));

        Assert.Contains("Unsupported provider 'HashiVault'", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddCashflowSecrets_ShouldThrow_WhenAzureKeyVaultConfigurationIsMissing()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfigurationManager(new Dictionary<string, string?>
        {
            ["Providers:Secrets"] = "AzureKeyVault"
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddCashflowSecrets(configuration));

        Assert.Contains("AzureKeyVault:VaultUri", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddRedisProviderDependencyInjection_ShouldThrow_WhenProviderIsUnsupported()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Providers:Cache"] = "Memcached"
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddRedisProviderDependencyInjection(configuration));

        Assert.Contains("Unsupported cache provider 'Memcached'", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddRedisProviderDependencyInjection_ShouldThrow_WhenAzureRedisEndpointIsMissing()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Providers:Cache"] = "AzureRedis"
        });

        var exception = Assert.ThrowsAny<ArgumentException>(() =>
            services.AddRedisProviderDependencyInjection(configuration));

        Assert.Contains("AzureRedis:Endpoint", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddMongoDbProviderDependencyInjection_ShouldThrow_WhenProviderIsUnsupported()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Providers:Document"] = "DynamoDb"
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddMongoDBProviderDependencyInjection(configuration, "cashflow-report"));

        Assert.Contains("Unsupported document provider 'DynamoDb'", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddMongoDbProviderDependencyInjection_ShouldThrow_WhenCosmosDbConnectionIsMissing()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Providers:Document"] = "CosmosDb"
        });

        var exception = Assert.ThrowsAny<ArgumentException>(() =>
            services.AddMongoDBProviderDependencyInjection(configuration, "cashflow-report"));

        Assert.Contains("CosmosDb:MongoDB:Connection", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddCashflowOpenTelemetryForWeb_ShouldThrow_WhenApplicationInsightsIsSelectedWithoutConnectionString()
    {
        var previousValue = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");

        try
        {
            Environment.SetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING", null);

            var services = new ServiceCollection();
            var configuration = BuildConfiguration(new Dictionary<string, string?>
            {
                ["Providers:Telemetry"] = "ApplicationInsights"
            });

            var exception = Assert.Throws<InvalidOperationException>(() =>
                services.AddCashflowOpenTelemetryForWeb(configuration, "cashflow-test-api"));

            Assert.Contains("ApplicationInsights:ConnectionString", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING", previousValue);
        }
    }

    [Fact]
    public void AddCashflowOpenTelemetryForWeb_ShouldThrow_WhenTelemetryProviderIsUnsupported()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Providers:Telemetry"] = "Datadog"
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddCashflowOpenTelemetryForWeb(configuration, "cashflow-test-api"));

        Assert.Contains("Unsupported telemetry provider 'Datadog'", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddCashflowOpenTelemetryForWeb_ShouldAcceptDefaultJaegerProvider()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>());

        services.AddCashflowOpenTelemetryForWeb(configuration, "cashflow-test-api");

        Assert.NotEmpty(services);
    }

    [Fact]
    public async Task GatewayConfigurationHealthCheck_ShouldBeHealthy_WhenEntraIdIsConfigured()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Providers:Identity"] = "EntraId",
            ["EntraId:TenantId"] = "tenant-id",
            ["EntraId:ClientId"] = "client-id",
            ["EntraId:Audience"] = "api://cashflow-api",
            ["ReverseProxy:Clusters:transaction-cluster:Destinations:transaction-api:Address"] = "https://transaction",
            ["ReverseProxy:Clusters:balance-query-cluster:Destinations:balance-query-api:Address"] = "https://balance"
        });

        var sut = new GatewayConfigurationHealthCheck(configuration);

        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task GatewayConfigurationHealthCheck_ShouldBeHealthy_WhenKeycloakAndLocalSecretsAreConfigured()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Keycloak:Authority"] = "https://keycloak",
            ["Keycloak:Audience"] = "cashflow-api",
            ["ReverseProxy:Clusters:transaction-cluster:Destinations:transaction-api:Address"] = "https://transaction",
            ["ReverseProxy:Clusters:balance-query-cluster:Destinations:balance-query-api:Address"] = "https://balance"
        });

        var sut = new GatewayConfigurationHealthCheck(configuration);

        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task GatewayConfigurationHealthCheck_ShouldBeUnhealthy_WhenSelectedIdentityProviderIsIncomplete()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Providers:Identity"] = "EntraId",
            ["EntraId:TenantId"] = "tenant-id",
            ["ReverseProxy:Clusters:transaction-cluster:Destinations:transaction-api:Address"] = "https://transaction",
            ["ReverseProxy:Clusters:balance-query-cluster:Destinations:balance-query-api:Address"] = "https://balance"
        });

        var sut = new GatewayConfigurationHealthCheck(configuration);

        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("EntraId/Local/ReverseProxy", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GatewayConfigurationHealthCheck_ShouldBeUnhealthy_WhenAzureKeyVaultIsSelectedWithoutVaultUri()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Providers:Identity"] = "Keycloak",
            ["Providers:Secrets"] = "AzureKeyVault",
            ["Keycloak:Authority"] = "https://keycloak",
            ["Keycloak:Audience"] = "cashflow-api",
            ["ReverseProxy:Clusters:transaction-cluster:Destinations:transaction-api:Address"] = "https://transaction",
            ["ReverseProxy:Clusters:balance-query-cluster:Destinations:balance-query-api:Address"] = "https://balance"
        });

        var sut = new GatewayConfigurationHealthCheck(configuration);

        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("Keycloak/AzureKeyVault/ReverseProxy", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GatewayConfigurationHealthCheck_ShouldBeUnhealthy_WhenProviderSelectionIsUnsupported()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Providers:Identity"] = "Auth0",
            ["ReverseProxy:Clusters:transaction-cluster:Destinations:transaction-api:Address"] = "https://transaction",
            ["ReverseProxy:Clusters:balance-query-cluster:Destinations:balance-query-api:Address"] = "https://balance"
        });

        var sut = new GatewayConfigurationHealthCheck(configuration);

        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
        Assert.Contains("unsupported provider", result.Exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── AddCashflowStorage ────────────────────────────────────────────────────

    [Fact]
    public void AddCashflowStorage_ShouldRegisterLocalReportArtifactStore_WhenProviderIsDefault()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>());

        services.AddCashflowStorage(configuration);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IReportArtifactStore) &&
            d.ImplementationType == typeof(LocalReportArtifactStore));
    }

    [Fact]
    public void AddCashflowStorage_ShouldRegisterLocalReportArtifactStore_WhenProviderIsExplicitlyLocal()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Providers:Storage"] = "Local"
        });

        services.AddCashflowStorage(configuration);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IReportArtifactStore) &&
            d.ImplementationType == typeof(LocalReportArtifactStore));
    }

    [Fact]
    public void AddCashflowStorage_ShouldRegisterAzureBlobReportArtifactStore_WhenProviderIsAzureBlob()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Providers:Storage"] = "AzureBlob",
            ["AzureBlob:ConnectionString"] = "DefaultEndpointsProtocol=https;AccountName=cashflow;AccountKey=dGVzdA==;EndpointSuffix=core.windows.net"
        });

        services.AddCashflowStorage(configuration);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IReportArtifactStore) &&
            d.ImplementationType == typeof(AzureBlobReportArtifactStore));
    }

    [Fact]
    public void AddCashflowStorage_ShouldRegisterAzureBlobReportArtifactStore_WhenManagedIdentityAndAccountNameAreConfigured()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Providers:Storage"] = "AzureBlob",
            ["AzureBlob:UseManagedIdentity"] = "true",
            ["AzureBlob:AccountName"] = "cashflowstorage"
        });

        services.AddCashflowStorage(configuration);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IReportArtifactStore) &&
            d.ImplementationType == typeof(AzureBlobReportArtifactStore));
    }

    [Fact]
    public void AddCashflowStorage_ShouldThrow_WhenAzureBlobConnectionStringIsMissing()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Providers:Storage"] = "AzureBlob"
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddCashflowStorage(configuration));

        Assert.Contains("AzureBlob:ConnectionString", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddCashflowStorage_ShouldThrow_WhenManagedIdentitySelectedButAccountNameIsMissing()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Providers:Storage"] = "AzureBlob",
            ["AzureBlob:UseManagedIdentity"] = "true"
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddCashflowStorage(configuration));

        Assert.Contains("AzureBlob:AccountName", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddCashflowStorage_ShouldThrow_WhenProviderIsUnsupported()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Providers:Storage"] = "S3"
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddCashflowStorage(configuration));

        Assert.Contains("Unsupported storage provider 'S3'", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddCashflowStorage_ShouldApplyCustomBasePath_WhenLocalStorageBasePathIsConfigured()
    {
        var services = new ServiceCollection();
        var customPath = Path.Combine(Path.GetTempPath(), "cashflow-custom-reports");
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["LocalStorage:BasePath"] = customPath
        });

        services.AddCashflowStorage(configuration);

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<LocalStorageOptions>();
        Assert.Equal(customPath, options.BasePath);
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static ConfigurationManager BuildConfigurationManager(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(values);
        return configuration;
    }
}
