using Cashflow.Gateway;
using Cashflow.Shared.Infrastructure.DependencyInjection;
using Cashflow.Shared.Messaging.Abstractions;
using Cashflow.Shared.Messaging.AzureServiceBus.MessageBus;
using Cashflow.Shared.Messaging.RabbitMQ.MessageBus;
using Cashflow.Shared.NoSql.MongoDB;
using Cashflow.Shared.NoSql.Redis;
using Cashflow.Shared.Observability;
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
        Assert.Contains("EntraId/ReverseProxy", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
