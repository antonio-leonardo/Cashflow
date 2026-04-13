using Cashflow.Shared.Storage.Abstractions;

namespace Cashflow.Shared.Storage.Local
{
    /// <summary>
    /// File-system backed implementation of <see cref="IReportArtifactStore"/>.
    /// Artifacts are stored under <see cref="LocalStorageOptions.BasePath"/>.
    /// The "download URI" is a <c>file://</c> URL — suitable for local dev only.
    /// </summary>
    public sealed class LocalReportArtifactStore : IReportArtifactStore
    {
        private readonly string _basePath;

        public LocalReportArtifactStore(LocalStorageOptions options)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(options.BasePath, nameof(options.BasePath));
            _basePath = options.BasePath;
        }

        public async Task<ReportArtifactMetadata> UploadAsync(
            string path,
            Stream content,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            var fullPath = Resolve(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            var storedAt = DateTimeOffset.UtcNow;

            await using var file = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await content.CopyToAsync(file, cancellationToken);
            await file.FlushAsync(cancellationToken);
            File.SetLastWriteTimeUtc(fullPath, storedAt.UtcDateTime);

            var fileInfo = new FileInfo(fullPath);
            return new ReportArtifactMetadata(
                Path: path,
                ContentType: contentType,
                SizeBytes: fileInfo.Length,
                CreatedAt: storedAt,
                Version: CreateVersionToken(fileInfo, storedAt));
        }

        public Task<Stream> DownloadAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            var fullPath = Resolve(path);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Artifact not found: {path}", fullPath);
            }

            Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Task.FromResult(stream);
        }

        public Task<Uri> GetDownloadUriAsync(
            string path,
            TimeSpan? expiry = null,
            CancellationToken cancellationToken = default)
        {
            // For local storage, return a file:// URI. The expiry param is intentionally ignored.
            var fullPath = Resolve(path);
            return Task.FromResult(new UriBuilder(Uri.UriSchemeFile, string.Empty)
            {
                Path = fullPath
            }.Uri);
        }

        public Task<bool> ExistsAsync(
            string path,
            CancellationToken cancellationToken = default)
            => Task.FromResult(File.Exists(Resolve(path)));

        private static string CreateVersionToken(FileInfo fileInfo, DateTimeOffset storedAt)
            => $"local-{storedAt.ToUnixTimeMilliseconds():x}-{fileInfo.Length:x}";

        private string Resolve(string path)
            => Path.Combine(_basePath, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
    }
}
