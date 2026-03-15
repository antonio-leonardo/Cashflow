namespace Cashflow.Back.End.Shared.Messaging.Abstractions
{
    public sealed record MessageMetadata(
    string CorrelationId,
    string CausationId,
    string Source,
    string? TenantId,
    DateTime CreatedAtUtc);
}