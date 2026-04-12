using Cashflow.Shared.Storage.Local;
using Cashflow.Shared.Storage.Abstractions;
using System.Text;

namespace Storage.Integration.Tests
{
    /// <summary>
    /// Integration tests for <see cref="LocalReportArtifactStore"/>.
    /// No Docker dependency — runs against the file system in a temp directory.
    /// </summary>
    public class LocalStoreIntegrationTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly IReportArtifactStore _store;

        public LocalStoreIntegrationTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"cashflow-local-store-tests-{Guid.NewGuid():N}");
            _store   = new LocalReportArtifactStore(new LocalStorageOptions { BasePath = _tempDir });
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }

        [Fact]
        public async Task UploadAsync_CreatesFile_AndReturnsPath()
        {
            var path  = $"account1/2024/01/report.csv";
            var bytes = Encoding.UTF8.GetBytes("id,amount");

            using var stream = new MemoryStream(bytes);
            var returned = await _store.UploadAsync(path, stream, "text/csv");

            Assert.Equal(path, returned);
            Assert.True(File.Exists(Path.Combine(_tempDir, "account1", "2024", "01", "report.csv")));
        }

        [Fact]
        public async Task ExistsAsync_ReturnsFalse_BeforeUpload()
        {
            Assert.False(await _store.ExistsAsync("nonexistent/file.csv"));
        }

        [Fact]
        public async Task ExistsAsync_ReturnsTrue_AfterUpload()
        {
            var path = $"{Guid.NewGuid():N}/file.csv";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("x"));
            await _store.UploadAsync(path, stream, "text/csv");

            Assert.True(await _store.ExistsAsync(path));
        }

        [Fact]
        public async Task DownloadAsync_ReturnsOriginalContent()
        {
            var path    = $"{Guid.NewGuid():N}/data.csv";
            var payload = "col1,col2\na,b";

            using var upload = new MemoryStream(Encoding.UTF8.GetBytes(payload));
            await _store.UploadAsync(path, upload, "text/csv");

            await using var download = await _store.DownloadAsync(path);
            using var reader = new StreamReader(download);
            Assert.Equal(payload, await reader.ReadToEndAsync());
        }

        [Fact]
        public async Task DownloadAsync_ThrowsFileNotFoundException_ForMissingFile()
        {
            await Assert.ThrowsAsync<FileNotFoundException>(
                () => _store.DownloadAsync("ghost/report.csv"));
        }

        [Fact]
        public async Task GetDownloadUriAsync_ReturnsFileUri()
        {
            var path = $"{Guid.NewGuid():N}/uri-test.csv";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("x"));
            await _store.UploadAsync(path, stream, "text/csv");

            var uri = await _store.GetDownloadUriAsync(path);

            Assert.Equal("file", uri.Scheme);
        }

        [Fact]
        public async Task UploadAsync_OverwritesExistingFile()
        {
            var path = $"{Guid.NewGuid():N}/overwrite.csv";

            using var first = new MemoryStream(Encoding.UTF8.GetBytes("first"));
            await _store.UploadAsync(path, first, "text/csv");

            using var second = new MemoryStream(Encoding.UTF8.GetBytes("second"));
            await _store.UploadAsync(path, second, "text/csv");

            await using var download = await _store.DownloadAsync(path);
            using var reader = new StreamReader(download);
            Assert.Equal("second", await reader.ReadToEndAsync());
        }
    }
}
