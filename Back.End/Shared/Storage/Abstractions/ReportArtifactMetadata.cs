namespace Cashflow.Shared.Storage.Abstractions
{
    /// <summary>Metadata attached to a stored report artifact.</summary>
    public sealed record ReportArtifactMetadata(
        string Path,
        string ContentType,
        long SizeBytes,
        DateTimeOffset CreatedAt,
        string? Version = null);
}
