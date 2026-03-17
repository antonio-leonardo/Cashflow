namespace Cashflow.Shared.Messaging.Abstractions
{
    public sealed record MessageMetadata(
    string CorrelationId,
    string CausationId,
    string Source,
    string? TenantId,
    DateTime CreatedAtUtc);
}