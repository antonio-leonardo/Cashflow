using Cashflow.Service.Transaction.Domain;
using Cashflow.Worker.Balance;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using System.Globalization;

namespace Balance.Domain.Tests;

public class TransactionCreatedHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldEvaluateAtomicScript_WithProvidedIdempotencyKey()
    {
        var accountId = Guid.NewGuid();
        var idempotencyKey = Guid.NewGuid().ToString("N");
        var databaseMock = new Mock<IDatabase>();
        databaseMock
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(1L));

        var muxMock = new Mock<IConnectionMultiplexer>();
        muxMock
            .Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
            .Returns(databaseMock.Object);

        var loggerMock = new Mock<ILogger<TransactionCreatedHandler>>();
        var repository = new RedisBalanceRepository(muxMock.Object);
        var sut = new TransactionCreatedHandler(repository, loggerMock.Object);
        var evt = new TransactionCreatedEventV1(
            Guid.NewGuid(),
            accountId,
            10m,
            "BRL",
            TransactionType.Credit);

        await sut.HandleAsync(evt, idempotencyKey, CancellationToken.None);

        var expectedProcessedKey = $"processed:balance-worker:{idempotencyKey}";
        var expectedTtlSeconds = ((int)TimeSpan.FromDays(30).TotalSeconds).ToString(CultureInfo.InvariantCulture);

        databaseMock.Verify(db => db.ScriptEvaluateAsync(
            It.Is<string>(script => script.Contains("'NX'", StringComparison.Ordinal)),
            It.Is<RedisKey[]>(keys =>
                keys.Length == 3 &&
                keys[0] == expectedProcessedKey &&
                keys[1] == $"balance:{accountId}" &&
                keys[2].ToString().StartsWith($"balance:daily:{accountId}:", StringComparison.Ordinal)),
            It.Is<RedisValue[]>(values =>
                values.Length == 7 &&
                values[0] == expectedTtlSeconds),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldFallbackToEventId_WhenIdempotencyKeyIsMissing()
    {
        var accountId = Guid.NewGuid();
        var databaseMock = new Mock<IDatabase>();
        databaseMock
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(1L));

        var muxMock = new Mock<IConnectionMultiplexer>();
        muxMock
            .Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
            .Returns(databaseMock.Object);

        var loggerMock = new Mock<ILogger<TransactionCreatedHandler>>();
        var repository = new RedisBalanceRepository(muxMock.Object);
        var sut = new TransactionCreatedHandler(repository, loggerMock.Object);
        var evt = new TransactionCreatedEventV1(
            Guid.NewGuid(),
            accountId,
            10m,
            "BRL",
            TransactionType.Debit);

        await sut.HandleAsync(evt, null, CancellationToken.None);

        var expectedProcessedKey = $"processed:balance-worker:{evt.EventId.ToString("N")}";

        databaseMock.Verify(db => db.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.Is<RedisKey[]>(keys =>
                keys.Length == 3 &&
                keys[0] == expectedProcessedKey),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }
}
