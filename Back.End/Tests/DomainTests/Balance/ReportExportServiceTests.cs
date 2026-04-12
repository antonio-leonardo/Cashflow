using Cashflow.Shared.Storage.Abstractions;
using Infrastructure.Test;
using MongoDB.Driver;
using Moq;

namespace Balance.Domain.Tests;

/// <summary>
/// Unit/integration tests for <see cref="Cashflow.Worker.Report.ReportExportService"/>.
///
/// Uses a real MongoDB container (via <see cref="MongoDbContainerFixture"/>) to avoid mocking
/// the complex MongoDB fluent-find chain, while mocking <see cref="IReportArtifactStore"/>
/// to keep storage assertions fast and deterministic.
/// </summary>
[Collection("MongoDbCollection")]
public class ReportExportServiceTests
{
    private readonly MongoDbContainerFixture _mongo;

    public ReportExportServiceTests(MongoDbContainerFixture mongo)
    {
        _mongo = mongo;
    }

    private IMongoDatabase GetDatabase() =>
        new MongoClient(_mongo.ConnectionString).GetDatabase($"report-export-{Guid.NewGuid():N}");

    // ── CSV path generation ───────────────────────────────────────────────────

    [Fact]
    public async Task ExportDailyAsync_ReturnsCorrectPath_ForGivenAccountAndDate()
    {
        var db        = GetDatabase();
        var store     = new Mock<IReportArtifactStore>();
        var accountId = Guid.NewGuid();
        var date      = new DateOnly(2025, 6, 15);

        store.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((string p, Stream _, string _, CancellationToken _) => p);

        store.Setup(s => s.GetDownloadUriAsync(It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new Uri("https://example.com/report.csv"));

        var sut    = new Cashflow.Worker.Report.ReportExportService(db, store.Object);
        var result = await sut.ExportDailyAsync(accountId, date);

        Assert.Equal($"{accountId}/2025/06/15/report.csv", result.Path);
    }

    [Fact]
    public async Task ExportDailyAsync_TransactionCount_IsZero_WhenCollectionIsEmpty()
    {
        var db        = GetDatabase();
        var store     = new Mock<IReportArtifactStore>();

        store.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((string p, Stream _, string _, CancellationToken _) => p);

        store.Setup(s => s.GetDownloadUriAsync(It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new Uri("https://example.com/report.csv"));

        var sut    = new Cashflow.Worker.Report.ReportExportService(db, store.Object);
        var result = await sut.ExportDailyAsync(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow));

        Assert.Equal(0, result.TransactionCount);
    }

    [Fact]
    public async Task ExportDailyAsync_TransactionCount_MatchesDocumentsForDate()
    {
        var db        = GetDatabase();
        var store     = new Mock<IReportArtifactStore>();
        var accountId = Guid.NewGuid();
        var date      = DateOnly.FromDateTime(DateTime.UtcNow);
        var from      = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        // Insert 3 documents matching the date range
        var collection = db.GetCollection<Cashflow.Worker.Report.TransactionReportDocument>("transactions");
        await collection.InsertManyAsync(new[]
        {
            new Cashflow.Worker.Report.TransactionReportDocument
            {
                Id        = Guid.NewGuid(),
                AccountId = accountId,
                Amount    = 100m,
                Currency  = "BRL",
                CreatedAt = from.AddHours(1)
            },
            new Cashflow.Worker.Report.TransactionReportDocument
            {
                Id        = Guid.NewGuid(),
                AccountId = accountId,
                Amount    = 250m,
                Currency  = "BRL",
                CreatedAt = from.AddHours(2)
            },
            new Cashflow.Worker.Report.TransactionReportDocument
            {
                Id        = Guid.NewGuid(),
                AccountId = accountId,
                Amount    = 75m,
                Currency  = "BRL",
                CreatedAt = from.AddHours(3)
            }
        });

        // One document from a different account — must not be counted
        await collection.InsertOneAsync(new Cashflow.Worker.Report.TransactionReportDocument
        {
            Id        = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            Amount    = 999m,
            Currency  = "BRL",
            CreatedAt = from.AddHours(1)
        });

        store.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((string p, Stream _, string _, CancellationToken _) => p);

        store.Setup(s => s.GetDownloadUriAsync(It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new Uri("https://example.com/report.csv"));

        var sut    = new Cashflow.Worker.Report.ReportExportService(db, store.Object);
        var result = await sut.ExportDailyAsync(accountId, date);

        Assert.Equal(3, result.TransactionCount);
    }

    // ── Upload call ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ExportDailyAsync_CallsUploadAsync_WithCsvContentType()
    {
        var db    = GetDatabase();
        var store = new Mock<IReportArtifactStore>();

        store.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((string p, Stream _, string _, CancellationToken _) => p);

        store.Setup(s => s.GetDownloadUriAsync(It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new Uri("https://example.com/report.csv"));

        var sut = new Cashflow.Worker.Report.ReportExportService(db, store.Object);
        await sut.ExportDailyAsync(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow));

        store.Verify(s => s.UploadAsync(
            It.IsAny<string>(),
            It.IsAny<Stream>(),
            "text/csv",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Download URI passthrough ──────────────────────────────────────────────

    [Fact]
    public async Task ExportDailyAsync_DownloadUri_MatchesStoreResponse()
    {
        var db             = GetDatabase();
        var store          = new Mock<IReportArtifactStore>();
        var expectedUri    = new Uri("https://reports.example.com/file.csv?sas=abc");

        store.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((string p, Stream _, string _, CancellationToken _) => p);

        store.Setup(s => s.GetDownloadUriAsync(It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(expectedUri);

        var sut    = new Cashflow.Worker.Report.ReportExportService(db, store.Object);
        var result = await sut.ExportDailyAsync(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow));

        Assert.Equal(expectedUri, result.DownloadUri);
    }

    [Fact]
    public async Task ExportDailyAsync_PassesCustomExpiry_ToGetDownloadUriAsync()
    {
        var db      = GetDatabase();
        var store   = new Mock<IReportArtifactStore>();
        var expiry  = TimeSpan.FromHours(24);

        store.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((string p, Stream _, string _, CancellationToken _) => p);

        store.Setup(s => s.GetDownloadUriAsync(It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new Uri("https://example.com/report.csv"));

        var sut = new Cashflow.Worker.Report.ReportExportService(db, store.Object);
        await sut.ExportDailyAsync(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow), expiry);

        store.Verify(s => s.GetDownloadUriAsync(
            It.IsAny<string>(),
            expiry,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExportDailyAsync_DefaultExpiry_IsOneHour()
    {
        var db    = GetDatabase();
        var store = new Mock<IReportArtifactStore>();

        store.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((string p, Stream _, string _, CancellationToken _) => p);

        store.Setup(s => s.GetDownloadUriAsync(It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new Uri("https://example.com/report.csv"));

        var sut = new Cashflow.Worker.Report.ReportExportService(db, store.Object);
        await sut.ExportDailyAsync(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow));

        store.Verify(s => s.GetDownloadUriAsync(
            It.IsAny<string>(),
            TimeSpan.FromHours(1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Result metadata ───────────────────────────────────────────────────────

    [Fact]
    public async Task ExportDailyAsync_GeneratedAt_IsRecentUtcTimestamp()
    {
        var db    = GetDatabase();
        var store = new Mock<IReportArtifactStore>();
        var before = DateTimeOffset.UtcNow;

        store.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((string p, Stream _, string _, CancellationToken _) => p);

        store.Setup(s => s.GetDownloadUriAsync(It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new Uri("https://example.com/report.csv"));

        var sut    = new Cashflow.Worker.Report.ReportExportService(db, store.Object);
        var result = await sut.ExportDailyAsync(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow));
        var after  = DateTimeOffset.UtcNow;

        Assert.InRange(result.GeneratedAt, before, after);
    }
}
