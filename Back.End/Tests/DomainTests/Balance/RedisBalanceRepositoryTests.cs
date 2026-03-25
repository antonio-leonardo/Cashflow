using Cashflow.Service.Transaction.Domain;
using Cashflow.Worker.Balance;
using Moq;
using StackExchange.Redis;
using System.Globalization;

namespace Balance.Domain.Tests;

public class RedisBalanceRepositoryTests
{
    private readonly Mock<IConnectionMultiplexer> _multiplexerMock;
    private readonly Mock<IDatabase> _databaseMock;
    private readonly RedisBalanceRepository _sut;

    private readonly Dictionary<string, string> _lastHashValues = new(StringComparer.Ordinal);

    public RedisBalanceRepositoryTests()
    {
        _databaseMock = new Mock<IDatabase>();
        _multiplexerMock = new Mock<IConnectionMultiplexer>();

        _multiplexerMock
            .Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
            .Returns(_databaseMock.Object);

        _databaseMock
            .Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _databaseMock
            .Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _databaseMock
            .Setup(db => db.HashSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<HashEntry[]>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisKey, HashEntry[], CommandFlags>((_, entries, _) =>
            {
                _lastHashValues.Clear();
                foreach (var entry in entries)
                {
                    _lastHashValues[entry.Name.ToString()] = entry.Value.ToString();
                }
            })
            .Returns(Task.CompletedTask);

        _databaseMock
            .Setup(db => db.LockTakeAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _databaseMock
            .Setup(db => db.LockReleaseAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _sut = new RedisBalanceRepository(_multiplexerMock.Object);
    }

    [Fact]
    public async Task ApplyAsync_ShouldIncreaseNetAndCredits_ForCreditTransaction()
    {
        _databaseMock
            .Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync("10.00");

        _databaseMock
            .Setup(db => db.HashGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue[] { "5.00", "2.00", "3.00" });

        var evt = new TransactionCreatedEventV1(
            Guid.NewGuid(),
            Guid.NewGuid(),
            4.00m,
            "BRL",
            TransactionType.Credit);

        await _sut.ApplyAsync(evt);

        AssertStringSetWasCalled();
        AssertLockWasUsed();
        AssertHashValue("credits", 9.00m);
        AssertHashValue("debits", 2.00m);
        AssertHashValue("net", 7.00m);
    }

    [Fact]
    public async Task ApplyAsync_ShouldDecreaseNetAndIncreaseDebits_ForDebitTransaction()
    {
        _databaseMock
            .Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync("10.00");

        _databaseMock
            .Setup(db => db.HashGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue[] { "5.00", "2.00", "3.00" });

        var evt = new TransactionCreatedEventV1(
            Guid.NewGuid(),
            Guid.NewGuid(),
            4.00m,
            "BRL",
            TransactionType.Debit);

        await _sut.ApplyAsync(evt);

        AssertStringSetWasCalled();
        AssertLockWasUsed();
        AssertHashValue("credits", 5.00m);
        AssertHashValue("debits", 6.00m);
        AssertHashValue("net", -1.00m);
    }

    [Fact]
    public async Task ApplyAsync_ShouldUseZero_WhenRedisContainsInvalidString()
    {
        _databaseMock
            .Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync("invalid");

        _databaseMock
            .Setup(db => db.HashGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue[] { RedisValue.Null, RedisValue.Null, RedisValue.Null });

        var evt = new TransactionCreatedEventV1(
            Guid.NewGuid(),
            Guid.NewGuid(),
            3.25m,
            "BRL",
            TransactionType.Credit);

        await _sut.ApplyAsync(evt);

        AssertStringSetWasCalled();
        AssertLockWasUsed();
        AssertHashValue("credits", 3.25m);
        AssertHashValue("debits", 0.00m);
        AssertHashValue("net", 3.25m);
    }

    [Fact]
    public async Task ApplyAsync_ShouldThrowTimeout_WhenLockCannotBeAcquired()
    {
        _databaseMock
            .Setup(db => db.LockTakeAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(false);

        var accountId = Guid.NewGuid();
        var evt = new TransactionCreatedEventV1(
            Guid.NewGuid(),
            accountId,
            10.00m,
            "BRL",
            TransactionType.Credit);

        var exception = await Assert.ThrowsAsync<TimeoutException>(() => _sut.ApplyAsync(evt));

        Assert.Contains(accountId.ToString(), exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private void AssertHashValue(string field, decimal expected)
    {
        Assert.True(_lastHashValues.TryGetValue(field, out var rawValue));
        var parsed = decimal.Parse(rawValue, CultureInfo.InvariantCulture);
        Assert.Equal(expected, parsed);
    }

    private void AssertStringSetWasCalled()
    {
        Assert.Contains(_databaseMock.Invocations, invocation =>
            invocation.Method.Name == nameof(IDatabase.StringSetAsync));
    }

    private void AssertLockWasUsed()
    {
        Assert.Contains(_databaseMock.Invocations, invocation =>
            invocation.Method.Name == nameof(IDatabase.LockTakeAsync));
        Assert.Contains(_databaseMock.Invocations, invocation =>
            invocation.Method.Name == nameof(IDatabase.LockReleaseAsync));
    }
}
