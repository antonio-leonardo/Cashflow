namespace Cashflow.Shared.Contracts.Api
{
    public sealed record GetDailyReportExportResponse(
        Guid AccountId,
        DateOnly Date,
        string Path,
        string ContentType,
        long SizeBytes,
        DateTimeOffset StoredAt,
        string? Version,
        Uri DownloadUri,
        int TransactionCount,
        DateTimeOffset GeneratedAt);
}
