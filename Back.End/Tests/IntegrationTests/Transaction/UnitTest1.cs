using Cashflow.Service.Transaction.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Transaction.Integration.Tests;

public class UnitTest1
{
    [Fact]
    public async Task Should_Insert_Transaction_Into_Database()
    {
        var options = new DbContextOptionsBuilder<TransactionDbContext>()
            .UseInMemoryDatabase(databaseName: "transaction-test-db")
            .Options;

        var db = new TransactionDbContext(options);

        var entity = new TransactionEntity
        {
            Id = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            Amount = 100,
            Currency = "USD"
        };

        db.Transactions.Add(entity);
        await db.SaveChangesAsync();

        var saved = await db.Transactions.FirstOrDefaultAsync();

        Assert.NotNull(saved);
        Assert.Equal(entity.Amount, saved.Amount);
    }
}