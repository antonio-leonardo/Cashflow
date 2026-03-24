using Cashflow.Service.Transaction.Domain;

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
}