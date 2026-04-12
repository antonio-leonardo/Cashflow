using Cashflow.Shared.Storage.Abstractions;
using MongoDB.Driver;
using System.Globalization;
using System.Text;

namespace Cashflow.Worker.Report
{
    /// <summary>
    /// Generates a daily CSV report from the transaction projection and uploads it to
    /// <see cref="IReportArtifactStore"/>. Returns the path and a time-limited download URI.
    /// </summary>
    public sealed class ReportExportService
    {
        private readonly IMongoDatabase _database;
        private readonly IReportArtifactStore _store;

        public ReportExportService(IMongoDatabase database, IReportArtifactStore store)
        {
            _database = database;
            _store     = store;
        }

        public async Task<ReportExportResult> ExportDailyAsync(
            Guid accountId,
            DateOnly date,
            TimeSpan? downloadLinkExpiry = null,
            CancellationToken cancellationToken = default)
        {
            var collection = _database.GetCollection<TransactionReportDocument>("transactions");

            var from = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var to   = date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

            var transactions = await collection
                .Find(t => t.AccountId == accountId && t.CreatedAt >= from && t.CreatedAt <= to)
                .SortBy(t => t.CreatedAt)
                .ToListAsync(cancellationToken);

            var csv = BuildCsv(transactions);
            var path = $"{accountId}/{date:yyyy/MM/dd}/report.csv";

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
            await _store.UploadAsync(path, stream, "text/csv", cancellationToken);

            var downloadUri = await _store.GetDownloadUriAsync(
                path,
                downloadLinkExpiry ?? TimeSpan.FromHours(1),
                cancellationToken);

            return new ReportExportResult(
                Path: path,
                DownloadUri: downloadUri,
                TransactionCount: transactions.Count,
                GeneratedAt: DateTimeOffset.UtcNow);
        }

        private static string BuildCsv(IEnumerable<TransactionReportDocument> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Id,AccountId,Amount,Currency,CreatedAt");

            foreach (var row in rows)
            {
                sb.AppendLine(string.Join(',',
                    row.Id,
                    row.AccountId,
                    row.Amount.ToString(CultureInfo.InvariantCulture),
                    row.Currency,
                    row.CreatedAt.ToString("o", CultureInfo.InvariantCulture)));
            }

            return sb.ToString();
        }
    }

    public sealed record ReportExportResult(
        string Path,
        Uri DownloadUri,
        int TransactionCount,
        DateTimeOffset GeneratedAt);
}
