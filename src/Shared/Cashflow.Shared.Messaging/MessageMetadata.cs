namespace Cashflow.Shared.Messaging
{
    public sealed record MessageMetadata(
    string CorrelationId,
    string CausationId,
    string Source,
    string? TenantId,
    DateTime CreatedAtUtc);
}