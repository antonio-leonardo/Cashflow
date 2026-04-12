namespace Cashflow.Shared.Contracts.Api
{
    public sealed record GetDailyReportExportResponse(
        Guid AccountId,
        DateOnly Date,
        string Path,
        Uri DownloadUri,
        int TransactionCount,
        DateTimeOffset GeneratedAt);
}
