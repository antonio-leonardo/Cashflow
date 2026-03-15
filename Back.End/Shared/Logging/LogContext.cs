namespace Cashflow.Back.End.Shared.Logging
{
    public sealed record LogContext(
    string ServiceName,
    string? CorrelationId,
    string? TransactionId,
    string? UserId);
}