using Cashflow.Service.Transaction.Domain;

namespace Balance.Domain.Tests;

public class BalanceDomain
{
    [Fact]
    public void Should_Add_Transaction_Value_To_Balance()
    {
        decimal balance = 200m;

        var evt = new TransactionCreatedEventV1(
            Guid.NewGuid(),
            Guid.NewGuid(),
            50m,
            "USD");

        balance += evt.Amount;

        Assert.Equal(250m, balance);
    }
}