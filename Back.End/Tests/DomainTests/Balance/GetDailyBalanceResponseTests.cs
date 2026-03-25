using Cashflow.Shared.Contracts.Api;

namespace Balance.Domain.Tests;

public class GetDailyBalanceResponseTests
{
    [Fact]
    public void Constructor_ShouldAssignAllProperties()
    {
        var accountId = Guid.NewGuid();
        var date = new DateOnly(2026, 03, 25);

        var response = new GetDailyBalanceResponse(
            accountId,
            date,
            120.50m,
            20.10m,
            100.40m);

        Assert.Equal(accountId, response.AccountId);
        Assert.Equal(date, response.Date);
        Assert.Equal(120.50m, response.TotalCredits);
        Assert.Equal(20.10m, response.TotalDebits);
        Assert.Equal(100.40m, response.NetBalance);
    }
}
