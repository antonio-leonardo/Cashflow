using Cashflow.Service.Transaction.Infrastructure.Persistence;
using Cashflow.Service.Transaction.Postgres.DependencyInjection;
using Cashflow.Shared.Messaging.Abstractions;
using Cashflow.Shared.Messaging.RabbitMQ.DependencyInjection;
using Cashflow.Shared.Messaging.RabbitMQ.MessageBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Balance.Domain.Tests;

public class DependencyInjectionExtensionsTests
{
    [Fact]
    public void AddRabbitMQDependencyInjection_ShouldBindRabbitSectionAndRegisterBus()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["RabbitMq:Host"] = "localhost",
            ["RabbitMq:Port"] = "5673",
            ["RabbitMq:Username"] = "user",
            ["RabbitMq:Pwd"] = "secret-pwd",
            ["RabbitMq:ConsumerName"] = "balance-consumer"
        });

        var services = new ServiceCollection();
        services.AddRabbitMQDependencyInjection(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RabbitMqOptions>>().Value;

        Assert.Equal("localhost", options.Host);
        Assert.Equal(5673, options.Port);
        Assert.Equal("user", options.Username);
        Assert.Equal("secret-pwd", options.Password);
        Assert.Equal("balance-consumer", options.ConsumerName);
        Assert.Contains(services, d =>
            d.ServiceType == typeof(IMessageBus) &&
            d.ImplementationType == typeof(RabbitMqBus));
    }

    [Fact]
    public void AddRabbitMQDependencyInjection_ShouldFallbackToInfrastructureSection()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Infrastructure:RabbitMq:Host"] = "rabbitmq.internal",
            ["Infrastructure:RabbitMq:Port"] = "5672",
            ["Infrastructure:RabbitMq:Username"] = "guest",
            ["Infrastructure:RabbitMq:Password"] = "guest"
        });

        var services = new ServiceCollection();
        services.AddRabbitMQDependencyInjection(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RabbitMqOptions>>().Value;

        Assert.Equal("rabbitmq.internal", options.Host);
        Assert.Equal(5672, options.Port);
    }

    [Fact]
    public void AddPostgresProviderDependencyInjection_ShouldUseDirectConnectionString_WhenProvided()
    {
        var connectionString = "Host=localhost;Port=5432;Database=cashflow;Username=postgres;Password=postgres";
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = connectionString
        });

        var services = new ServiceCollection();
        services.AddPostgresProviderDependencyInjection(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var transactionDb = scope.ServiceProvider.GetRequiredService<TransactionDbContext>();
        var idempotencyDb = scope.ServiceProvider.GetRequiredService<IdempotencyDbContext>();

        Assert.Contains("Npgsql", transactionDb.Database.ProviderName, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Npgsql", idempotencyDb.Database.ProviderName, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(connectionString, transactionDb.Database.GetConnectionString());
    }

    [Fact]
    public void AddPostgresProviderDependencyInjection_ShouldBuildConnectionString_FromInfrastructureSection()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Infrastructure:Postgres:Host"] = "postgres",
            ["Infrastructure:Postgres:Port"] = "5432",
            ["Infrastructure:Postgres:Database"] = "cashflow",
            ["Infrastructure:Postgres:Username"] = "postgres",
            ["Infrastructure:Postgres:Pwd"] = "postgres"
        });

        var services = new ServiceCollection();
        services.AddPostgresProviderDependencyInjection(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var transactionDb = scope.ServiceProvider.GetRequiredService<TransactionDbContext>();
        var resolvedConnectionString = transactionDb.Database.GetConnectionString();

        Assert.Contains("Host=postgres", resolvedConnectionString, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Database=cashflow", resolvedConnectionString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddPostgresProviderDependencyInjection_ShouldThrow_WhenConfigurationIsMissing()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>());
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddPostgresProviderDependencyInjection(configuration));

        Assert.Contains("Postgres connection not configured", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
