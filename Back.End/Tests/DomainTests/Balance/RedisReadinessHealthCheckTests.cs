using Cashflow.Service.Balance.API.Healthchecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using StackExchange.Redis;

namespace Balance.Domain.Tests;

public class RedisReadinessHealthCheckTests
{
    private readonly Mock<IConnectionMultiplexer> _multiplexerMock;
    private readonly Mock<IDatabase> _databaseMock;
    private readonly RedisReadinessHealthCheck _sut;
    private readonly HealthCheckContext _context;

    public RedisReadinessHealthCheckTests()
    {
        _databaseMock    = new Mock<IDatabase>();
        _multiplexerMock = new Mock<IConnectionMultiplexer>();
        _multiplexerMock
            .Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
            .Returns(_databaseMock.Object);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Providers:Cache"] = "Redis"
            })
            .Build();

        _sut = new RedisReadinessHealthCheck(_multiplexerMock.Object, configuration);

        _context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration(
                "redis",
                _sut,
                HealthStatus.Unhealthy,
                null)
        };
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthy_WhenPingSucceeds()
    {
        _databaseMock
            .Setup(db => db.PingAsync(It.IsAny<CommandFlags>()))
            .ReturnsAsync(TimeSpan.FromMilliseconds(3));

        var result = await _sut.CheckHealthAsync(_context);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("ping succeeded", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenConnectionFails()
    {
        _databaseMock
            .Setup(db => db.PingAsync(It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(
                ConnectionFailureType.UnableToConnect,
                "Connection refused"));

        var result = await _sut.CheckHealthAsync(_context);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenGenericExceptionOccurs()
    {
        _databaseMock
            .Setup(db => db.PingAsync(It.IsAny<CommandFlags>()))
            .ThrowsAsync(new TimeoutException("Redis timeout"));

        var result = await _sut.CheckHealthAsync(_context);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.IsType<TimeoutException>(result.Exception);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenCacheProviderSelectionIsUnsupported()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Providers:Cache"] = "Memcached"
            })
            .Build();

        var sut = new RedisReadinessHealthCheck(_multiplexerMock.Object, configuration);

        var result = await sut.CheckHealthAsync(_context);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
        Assert.Contains("unsupported", result.Description, StringComparison.OrdinalIgnoreCase);
    }
}
