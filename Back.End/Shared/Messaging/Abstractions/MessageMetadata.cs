namespace Cashflow.Shared.Messaging.Abstractions
{
    public sealed record MessageMetadata(
        string CorrelationId,
        string CausationId,
        string Source,
        string? TenantId,
        DateTime CreatedAtUtc,
        string? TraceParent = null,
        string? Baggage = null,
        string? SessionId = null);
}
