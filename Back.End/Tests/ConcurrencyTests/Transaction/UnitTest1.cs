using Cashflow.Back.End.Service.Transaction.Domain;

namespace Transaction.Concurrency.Tests;

public class UnitTest1
{
    [Fact]
    public async Task Should_Handle_Multiple_Transactions_Concurrently()
    {
        var accountId = Guid.NewGuid();

        var tasks = Enumerable.Range(0, 50)
            .Select(_ => Task.Run(() =>
                new TransactionCreatedEventV1(
                    Guid.NewGuid(),
                    accountId,
                    10,
                    "USD")))
            .ToList();

        var events = await Task.WhenAll(tasks);

        Assert.Equal(50, events.Length);

        Assert.All(events, e =>
        {
            Assert.Equal(accountId, e.AccountId);
        });
    }
}
