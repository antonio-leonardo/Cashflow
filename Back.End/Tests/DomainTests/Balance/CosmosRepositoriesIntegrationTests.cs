using Cashflow.Service.Transaction.Domain;
using Cashflow.Shared.Storage.Abstractions;
using Cashflow.Worker.Audit;
using Cashflow.Worker.Report;
using Infrastructure.Test;
using Moq;
using MongoDB.Driver;

namespace Balance.Domain.Tests;

/// <summary>
/// Verifies that <see cref="MongoAuditRepository"/> and <see cref="MongoReportRepository"/>
/// honour their idempotency contracts when operating through the MongoDB wire protocol —
/// the same protocol used by the Azure Cosmos DB MongoDB API.
///
/// These tests intentionally reuse the plain <see cref="MongoDbContainerFixture"/> (mongo:7.0)
/// because the official Cosmos DB Emulator Docker image (mcr.microsoft.com/cosmosdb/linux/
/// azure-cosmos-emulator:mongodb) has a time-limited evaluation period that cannot be relied
/// on for automated CI.  Behaviour verified here is protocol-level idempotency, which is
/// identical across MongoDB and Cosmos DB MongoDB API endpoints.
///
/// Cosmos-specific integration (retryWrites=false, TLS port 10255, primary-key auth) is
/// covered by the azure-smoke pipeline stage that runs against a live Cosmos DB account.
/// </summary>
[Collection("MongoDbCollection")]
public sealed class CosmosRepositoriesIntegrationTests
{
    private readonly MongoDbContainerFixture _fixture;

    public CosmosRepositoriesIntegrationTests(MongoDbContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private IMongoDatabase GetDatabase(string name) =>
        new MongoClient(_fixture.ConnectionString).GetDatabase(name);

    private static TransactionCreatedEventV1 BuildEvent(decimal amount = 100m) =>
        new(Guid.NewGuid(), Guid.NewGuid(), amount, "BRL", TransactionType.Credit);

    [Fact]
    public async Task MongoAuditRepository_ShouldPersist_Idempotently_AgainstCosmosMongoApi()
    {
        var database = GetDatabase($"cosmos-audit-{Guid.NewGuid():N}");
        IAuditRepository repository = new MongoAuditRepository(database);
        var evt = BuildEvent();

        await repository.RecordAsync(evt);
        await repository.RecordAsync(evt);

        var collection = database.GetCollection<AuditDocument>("events");
        var count = await collection.CountDocumentsAsync(FilterDefinition<AuditDocument>.Empty);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task MongoReportRepository_ShouldPersist_Idempotently_AgainstCosmosMongoApi()
    {
        var database = GetDatabase($"cosmos-report-{Guid.NewGuid():N}");
        IReportRepository repository = new MongoReportRepository(database);
        var evt = BuildEvent(250m);

        await repository.AppendAsync(evt);
        await repository.AppendAsync(evt);

        var collection = database.GetCollection<TransactionReportDocument>("transactions");
        var count = await collection.CountDocumentsAsync(FilterDefinition<TransactionReportDocument>.Empty);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task ReportExportService_ShouldGenerate_Report_FromCosmosMongoApi()
    {
        var database = GetDatabase($"cosmos-export-{Guid.NewGuid():N}");
        var accountId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var collection = database.GetCollection<TransactionReportDocument>("transactions");
        await collection.InsertOneAsync(new TransactionReportDocument
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Amount = 150m,
            Currency = "BRL",
            CreatedAt = from.AddHours(1)
        });

        var store = new Mock<IReportArtifactStore>();
        store.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string path, Stream _, string contentType, CancellationToken _) =>
                new ReportArtifactMetadata(path, contentType, 256, DateTimeOffset.UtcNow, "cosmos-export-v1"));
        store.Setup(s => s.GetDownloadUriAsync(It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Uri("https://reports.example.com/daily.csv"));

        var sut = new ReportExportService(database, store.Object);
        var result = await sut.ExportDailyAsync(accountId, date);

        Assert.Equal(1, result.TransactionCount);
        Assert.Equal($"{accountId}/{date:yyyy/MM/dd}/report.csv", result.Path);
        Assert.Equal(new Uri("https://reports.example.com/daily.csv"), result.DownloadUri);
        Assert.Equal("cosmos-export-v1", result.Artifact.Version);
    }
}
