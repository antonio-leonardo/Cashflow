namespace Cashflow.Shared.Storage.Abstractions
{
    /// <summary>
    /// Stores and retrieves generated report artifacts (e.g., CSV, PDF).
    /// All paths use forward-slash notation: "{accountId}/{date}/{filename}".
    /// </summary>
    public interface IReportArtifactStore
    {
        /// <summary>Uploads report bytes and returns the stored artifact metadata.</summary>
        Task<ReportArtifactMetadata> UploadAsync(
            string path,
            Stream content,
            string contentType,
            CancellationToken cancellationToken = default);

        /// <summary>Downloads the artifact at the given path.</summary>
        Task<Stream> DownloadAsync(
            string path,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a time-limited read URL (SAS for Azure Blob, signed URL for local).
        /// Expiry defaults to 1 hour.
        /// </summary>
        Task<Uri> GetDownloadUriAsync(
            string path,
            TimeSpan? expiry = null,
            CancellationToken cancellationToken = default);

        /// <summary>Returns true when the artifact exists.</summary>
        Task<bool> ExistsAsync(
            string path,
            CancellationToken cancellationToken = default);
    }
}
