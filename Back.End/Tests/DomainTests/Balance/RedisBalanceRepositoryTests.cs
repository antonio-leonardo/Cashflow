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

    public RedisBalanceRepositoryTests()
    {
        _databaseMock = new Mock<IDatabase>();
        _multiplexerMock = new Mock<IConnectionMultiplexer>();

        _multiplexerMock
            .Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
            .Returns(_databaseMock.Object);

        _databaseMock
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(1L));

        _sut = new RedisBalanceRepository(_multiplexerMock.Object);
    }

    [Fact]
    public async Task ApplyAsync_ShouldExecuteIdempotentAtomicScript_ForCreditTransaction()
    {
        var accountId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var consumerName = "balance-worker";
        var idempotencyKey = Guid.NewGuid().ToString("N");
        var ttl = TimeSpan.FromDays(30);

        var evt = new TransactionCreatedEventV1(
            transactionId,
            accountId,
            4.00m,
            "BRL",
            TransactionType.Credit);

        var applied = await _sut.ApplyAsync(evt, consumerName, idempotencyKey, ttl);

        Assert.True(applied);

        var expectedTtlSeconds = ((int)Math.Ceiling(ttl.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        var expectedProcessedKey = $"processed:{consumerName}:{idempotencyKey}";

        _databaseMock.Verify(db => db.ScriptEvaluateAsync(
            It.Is<string>(script =>
                script.Contains("redis.call('SET', processedKey", StringComparison.Ordinal) &&
                script.Contains("'NX'", StringComparison.Ordinal) &&
                script.Contains("HINCRBYFLOAT", StringComparison.Ordinal)),
            It.Is<RedisKey[]>(keys =>
                keys.Length == 3 &&
                keys[0] == expectedProcessedKey &&
                keys[1] == $"balance:{accountId}" &&
                keys[2].ToString().StartsWith($"balance:daily:{accountId}:", StringComparison.Ordinal)),
            It.Is<RedisValue[]>(values =>
                values.Length == 7 &&
                values[0] == expectedTtlSeconds &&
                values[6] == "1"),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task ApplyAsync_ShouldFlagDebit_ForDebitTransaction()
    {
        var evt = new TransactionCreatedEventV1(
            Guid.NewGuid(),
            Guid.NewGuid(),
            4.00m,
            "BRL",
            TransactionType.Debit);

        var applied = await _sut.ApplyAsync(evt, "balance-worker", "idempotency-key", TimeSpan.FromDays(30));

        Assert.True(applied);

        _databaseMock.Verify(db => db.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.Is<RedisValue[]>(values => values.Length == 7 && values[6] == "0"),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task ApplyAsync_ShouldEnforceMinimumTtlOfOneSecond()
    {
        var evt = new TransactionCreatedEventV1(
            Guid.NewGuid(),
            Guid.NewGuid(),
            3.25m,
            "BRL",
            TransactionType.Credit);

        var applied = await _sut.ApplyAsync(evt, "balance-worker", "idempotency-key", TimeSpan.Zero);

        Assert.True(applied);

        _databaseMock.Verify(db => db.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.Is<RedisValue[]>(values => values.Length == 7 && values[0] == "1"),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task ApplyAsync_ShouldReturnFalse_WhenEventWasAlreadyProcessed()
    {
        _databaseMock
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(0L));

        var evt = new TransactionCreatedEventV1(
            Guid.NewGuid(),
            Guid.NewGuid(),
            7m,
            "BRL",
            TransactionType.Debit);

        var applied = await _sut.ApplyAsync(evt, "balance-worker", evt.EventId.ToString("N"), TimeSpan.FromDays(30));

        Assert.False(applied);
    }
}
