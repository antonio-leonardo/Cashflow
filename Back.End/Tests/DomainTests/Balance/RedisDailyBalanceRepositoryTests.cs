using Cashflow.Service.Balance.API.Repositories;
using Moq;
using StackExchange.Redis;

namespace Balance.Domain.Tests;

public class RedisDailyBalanceRepositoryTests
{
    private readonly Mock<IConnectionMultiplexer> _multiplexerMock;
    private readonly Mock<IDatabase> _databaseMock;
    private readonly RedisDailyBalanceRepository _sut;

    public RedisDailyBalanceRepositoryTests()
    {
        _databaseMock    = new Mock<IDatabase>();
        _multiplexerMock = new Mock<IConnectionMultiplexer>();
        _multiplexerMock
            .Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
            .Returns(_databaseMock.Object);
        _sut = new RedisDailyBalanceRepository(_multiplexerMock.Object);
    }

    [Fact]
    public async Task GetDailyBalanceAsync_ReturnsNull_WhenAllHashFieldsAreEmpty()
    {
        _databaseMock
            .Setup(db => db.HashGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue[] { RedisValue.Null, RedisValue.Null, RedisValue.Null });

        var result = await _sut.GetDailyBalanceAsync(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow));

        Assert.Null(result);
    }

    [Fact]
    public async Task GetDailyBalanceAsync_ReturnsSummary_WhenHashDataExists()
    {
        _databaseMock
            .Setup(db => db.HashGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue[] { "100.00", "50.00", "50.00" });

        var result = await _sut.GetDailyBalanceAsync(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow));

        Assert.NotNull(result);
        Assert.Equal(100.00m, result.TotalCredits);
        Assert.Equal(50.00m,  result.TotalDebits);
        Assert.Equal(50.00m,  result.NetBalance);
    }

    [Fact]
    public async Task GetDailyBalanceAsync_ThrowsOperationCanceledException_WhenTokenAlreadyCancelled()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _sut.GetDailyBalanceAsync(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow), cts.Token));
    }

    [Theory]
    [InlineData("200.00", "0.00",   "200.00")]
    [InlineData("0.00",   "75.50",  "-75.50")]
    [InlineData("300.25", "100.10", "200.15")]
    public async Task GetDailyBalanceAsync_ParsesAllDecimalFields(string credits, string debits, string net)
    {
        _databaseMock
            .Setup(db => db.HashGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue[] { credits, debits, net });

        var result = await _sut.GetDailyBalanceAsync(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow));

        Assert.NotNull(result);
        Assert.Equal(decimal.Parse(credits, System.Globalization.CultureInfo.InvariantCulture), result.TotalCredits);
        Assert.Equal(decimal.Parse(debits,  System.Globalization.CultureInfo.InvariantCulture), result.TotalDebits);
        Assert.Equal(decimal.Parse(net,     System.Globalization.CultureInfo.InvariantCulture), result.NetBalance);
    }

    [Fact]
    public async Task GetDailyBalanceAsync_ReturnsNull_WhenOnlyCreditsFieldIsAbsent()
    {
        _databaseMock
            .Setup(db => db.HashGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue[] { RedisValue.Null, RedisValue.Null, RedisValue.Null });

        var result = await _sut.GetDailyBalanceAsync(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow));

        Assert.Null(result);
    }

    [Fact]
    public async Task GetDailyBalanceAsync_UsesDailyKeyWithCorrectFormat()
    {
        var accountId     = Guid.NewGuid();
        var referenceDate = new DateOnly(2025, 3, 15);
        var expectedKey   = $"balance:daily:{accountId}:2025-03-15";

        _databaseMock
            .Setup(db => db.HashGetAsync(
                It.Is<RedisKey>(k => k == expectedKey),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue[] { "10.00", "5.00", "5.00" });

        var result = await _sut.GetDailyBalanceAsync(accountId, referenceDate);

        Assert.NotNull(result);
        _databaseMock.Verify(db => db.HashGetAsync(
            It.Is<RedisKey>(k => k == expectedKey),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }
}
