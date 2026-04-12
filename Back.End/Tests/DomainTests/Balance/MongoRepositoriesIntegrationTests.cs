using Cashflow.Service.Transaction.Domain;
using Cashflow.Worker.Audit;
using Cashflow.Worker.Report;
using Infrastructure.Test;
using MongoDB.Driver;

namespace Balance.Domain.Tests;

/// <summary>
/// Integration tests for <see cref="MongoAuditRepository"/> and
/// <see cref="MongoReportRepository"/> using a real MongoDB container.
///
/// These tests verify that both repositories honour their idempotency contracts
/// and implement the correct domain interfaces.
///
/// Uses the existing MongoDbContainerFixture (plain mongo:7.0 image, no TLS).
/// </summary>
[Collection("MongoDbCollection")]
public class MongoRepositoriesIntegrationTests
{
    private readonly MongoDbContainerFixture _fixture;

    public MongoRepositoriesIntegrationTests(MongoDbContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private IMongoDatabase GetDatabase(string name)
    {
        var client = new MongoClient(_fixture.ConnectionString);
        return client.GetDatabase(name);
    }

    private static TransactionCreatedEventV1 BuildEvent(decimal amount = 100m) =>
        new(Guid.NewGuid(), Guid.NewGuid(), amount, "BRL", TransactionType.Credit);

    // ── MongoAuditRepository ──────────────────────────────────────────────────

    [Fact]
    public void MongoAuditRepository_Implements_IAuditRepository()
    {
        var db   = GetDatabase("audit-test");
        var repo = new MongoAuditRepository(db);
        Assert.IsAssignableFrom<IAuditRepository>(repo);
    }

    [Fact]
    public async Task MongoAuditRepository_RecordAsync_InsertsDocument()
    {
        var db         = GetDatabase($"audit-{Guid.NewGuid():N}");
        IAuditRepository repo = new MongoAuditRepository(db);
        var evt        = BuildEvent();

        await repo.RecordAsync(evt);

        var collection = db.GetCollection<AuditDocument>("events");
        var count      = await collection.CountDocumentsAsync(FilterDefinition<AuditDocument>.Empty);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task MongoAuditRepository_RecordAsync_IsIdempotent_OnDuplicateEvent()
    {
        var db         = GetDatabase($"audit-{Guid.NewGuid():N}");
        IAuditRepository repo = new MongoAuditRepository(db);
        var evt        = BuildEvent();

        // First insert
        await repo.RecordAsync(evt);
        // Duplicate — must not throw
        await repo.RecordAsync(evt);

        var collection = db.GetCollection<AuditDocument>("events");
        var count      = await collection.CountDocumentsAsync(FilterDefinition<AuditDocument>.Empty);
        Assert.Equal(1, count);
    }

    // ── MongoReportRepository ─────────────────────────────────────────────────

    [Fact]
    public void MongoReportRepository_Implements_IReportRepository()
    {
        var db   = GetDatabase("report-test");
        var repo = new MongoReportRepository(db);
        Assert.IsAssignableFrom<IReportRepository>(repo);
    }

    [Fact]
    public async Task MongoReportRepository_AppendAsync_InsertsDocument()
    {
        var db         = GetDatabase($"report-{Guid.NewGuid():N}");
        IReportRepository repo = new MongoReportRepository(db);
        var evt        = BuildEvent(250m);

        await repo.AppendAsync(evt);

        var collection = db.GetCollection<Cashflow.Worker.Report.TransactionReportDocument>("transactions");
        var count      = await collection.CountDocumentsAsync(FilterDefinition<Cashflow.Worker.Report.TransactionReportDocument>.Empty);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task MongoReportRepository_AppendAsync_IsIdempotent_OnDuplicateEvent()
    {
        var db         = GetDatabase($"report-{Guid.NewGuid():N}");
        IReportRepository repo = new MongoReportRepository(db);
        var evt        = BuildEvent(50m);

        await repo.AppendAsync(evt);
        // Duplicate — must not throw
        await repo.AppendAsync(evt);

        var collection = db.GetCollection<Cashflow.Worker.Report.TransactionReportDocument>("transactions");
        var count      = await collection.CountDocumentsAsync(FilterDefinition<Cashflow.Worker.Report.TransactionReportDocument>.Empty);
        Assert.Equal(1, count);
    }
}
