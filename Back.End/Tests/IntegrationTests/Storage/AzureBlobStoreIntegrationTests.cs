using Azure.Storage.Blobs;
using Cashflow.Shared.Storage.AzureBlob;
using Cashflow.Shared.Storage.Abstractions;
using Infrastructure.Test;
using System.Text;

namespace Storage.Integration.Tests
{
    /// <summary>
    /// Integration tests for <see cref="AzureBlobReportArtifactStore"/> against Azurite.
    /// Covers upload, download, existence check and SAS URI generation.
    /// </summary>
    [Collection("AzuriteCollection")]
    [Trait("Category", "Azurite")]
    public class AzureBlobStoreIntegrationTests
    {
        private readonly AzuriteContainerFixture _fixture;

        public AzureBlobStoreIntegrationTests(AzuriteContainerFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task<IReportArtifactStore> CreateStoreAsync(string container = "reports")
        {
            // Ensure container exists before tests
            var serviceClient = new BlobServiceClient(_fixture.BlobConnectionString);
            var containerClient = serviceClient.GetBlobContainerClient(container);
            await containerClient.CreateIfNotExistsAsync();

            var options = new AzureBlobStorageOptions
            {
                ConnectionString = _fixture.BlobConnectionString,
                ContainerName    = container
            };
            return new AzureBlobReportArtifactStore(options);
        }

        [Fact]
        public async Task UploadAsync_ShouldStoreBlob_AndReturnPath()
        {
            var store   = await CreateStoreAsync();
            var path    = $"test/{Guid.NewGuid():N}/report.csv";
            var content = Encoding.UTF8.GetBytes("id,amount\n1,100");

            using var stream = new MemoryStream(content);
            var returned = await store.UploadAsync(path, stream, "text/csv");

            Assert.Equal(path, returned);
        }

        [Fact]
        public async Task ExistsAsync_ShouldReturnTrue_AfterUpload()
        {
            var store = await CreateStoreAsync();
            var path  = $"test/{Guid.NewGuid():N}/exists.csv";

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("data"));
            await store.UploadAsync(path, stream, "text/csv");

            Assert.True(await store.ExistsAsync(path));
        }

        [Fact]
        public async Task ExistsAsync_ShouldReturnFalse_ForMissingBlob()
        {
            var store = await CreateStoreAsync();
            Assert.False(await store.ExistsAsync($"nonexistent/{Guid.NewGuid():N}/file.csv"));
        }

        [Fact]
        public async Task DownloadAsync_ShouldReturnOriginalContent()
        {
            var store   = await CreateStoreAsync();
            var path    = $"test/{Guid.NewGuid():N}/download.csv";
            var payload = "header1,header2\nval1,val2";

            using var uploadStream = new MemoryStream(Encoding.UTF8.GetBytes(payload));
            await store.UploadAsync(path, uploadStream, "text/csv");

            await using var downloaded = await store.DownloadAsync(path);
            using var reader = new StreamReader(downloaded);
            var result = await reader.ReadToEndAsync();

            Assert.Equal(payload, result);
        }

        [Fact]
        public async Task GetDownloadUriAsync_ShouldReturnAbsoluteUri()
        {
            var store = await CreateStoreAsync();
            var path  = $"test/{Guid.NewGuid():N}/sas.csv";

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("content"));
            await store.UploadAsync(path, stream, "text/csv");

            var uri = await store.GetDownloadUriAsync(path, TimeSpan.FromMinutes(30));

            Assert.NotNull(uri);
            Assert.True(uri.IsAbsoluteUri);
        }

        [Fact]
        public async Task UploadAsync_ThenDownload_RoundTrip_PreservesBytes()
        {
            var store   = await CreateStoreAsync();
            var path    = $"test/{Guid.NewGuid():N}/roundtrip.bin";
            var original = Encoding.UTF8.GetBytes("roundtrip-payload-€-áéí");

            using var upload = new MemoryStream(original);
            await store.UploadAsync(path, upload, "application/octet-stream");

            await using var download = await store.DownloadAsync(path);
            using var ms = new MemoryStream();
            await download.CopyToAsync(ms);

            Assert.Equal(original, ms.ToArray());
        }
    }
}
