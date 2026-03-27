using Cashflow.Service.Transaction.Domain;
using System.Text.Json;

namespace Transaction.Domain.Tests;

public class UnitTest1
{
    [Fact]
    public void Should_Create_Transaction_With_Valid_Data()
    {
        var transactionId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var amount = 100;
        var currency = "BRL";

        var evt = new TransactionCreatedEventV1(
            transactionId,
            accountId,
            amount,
            currency,
            TransactionType.Credit);

        Assert.Equal(transactionId, evt.TransactionId);
        Assert.Equal(accountId, evt.AccountId);
        Assert.Equal(amount, evt.Amount);
        Assert.Equal(currency, evt.Currency);
        Assert.NotEqual(Guid.Empty, evt.EventId);
    }

    [Fact]
    public void Should_Preserve_Event_Metadata_On_Json_Roundtrip()
    {
        var evt = new TransactionCreatedEventV1(
            transactionId: Guid.NewGuid(),
            accountId: Guid.NewGuid(),
            amount: 100,
            currency: "BRL",
            type: TransactionType.Credit);

        var payload = JsonSerializer.Serialize(evt);
        var deserialized = JsonSerializer.Deserialize<TransactionCreatedEventV1>(payload);

        Assert.NotNull(deserialized);
        Assert.Equal(evt.EventId, deserialized!.EventId);
        Assert.Equal(evt.OccurredAt, deserialized.OccurredAt);
        Assert.Equal(evt.CorrelationId, deserialized.CorrelationId);
        Assert.Equal(evt.EventType, deserialized.EventType);
        Assert.Equal(evt.Version, deserialized.Version);
    }
}
