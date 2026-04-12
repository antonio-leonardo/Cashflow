using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Cashflow.Shared.Storage.Abstractions;

namespace Cashflow.Shared.Storage.AzureBlob
{
    public sealed class AzureBlobReportArtifactStore : IReportArtifactStore
    {
        private readonly BlobContainerClient _container;

        public AzureBlobReportArtifactStore(AzureBlobStorageOptions options)
        {
            _container = CreateContainerClient(options);
        }

        public async Task<string> UploadAsync(
            string path,
            Stream content,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            var blob = _container.GetBlobClient(path);

            await blob.UploadAsync(content, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
            }, cancellationToken);

            return path;
        }

        public async Task<Stream> DownloadAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            var blob = _container.GetBlobClient(path);
            var response = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);
            return response.Value.Content;
        }

        public Task<Uri> GetDownloadUriAsync(
            string path,
            TimeSpan? expiry = null,
            CancellationToken cancellationToken = default)
        {
            var blob = _container.GetBlobClient(path);
            var expiresOn = DateTimeOffset.UtcNow.Add(expiry ?? TimeSpan.FromHours(1));

            Uri sasUri;

            if (blob.CanGenerateSasUri)
            {
                // Key-based auth: generate SAS directly from the client.
                var sasBuilder = new BlobSasBuilder(BlobSasPermissions.Read, expiresOn)
                {
                    BlobContainerName = _container.Name,
                    BlobName = path,
                    Resource = "b"
                };

                sasUri = blob.GenerateSasUri(sasBuilder);
            }
            else
            {
                // Managed Identity path: return the blob URI without SAS
                // (caller must be authorised via Azure RBAC).
                sasUri = blob.Uri;
            }

            return Task.FromResult(sasUri);
        }

        public async Task<bool> ExistsAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            var blob = _container.GetBlobClient(path);
            var response = await blob.ExistsAsync(cancellationToken);
            return response.Value;
        }

        private static BlobContainerClient CreateContainerClient(AzureBlobStorageOptions options)
        {
            BlobServiceClient serviceClient;

            if (options.UseManagedIdentity)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(options.AccountName,
                    "AzureBlob:AccountName is required when UseManagedIdentity is true.");

                serviceClient = new BlobServiceClient(
                    new Uri($"https://{options.AccountName}.blob.core.windows.net"),
                    new DefaultAzureCredential());
            }
            else
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(options.ConnectionString,
                    "AzureBlob:ConnectionString is required when UseManagedIdentity is false.");

                serviceClient = new BlobServiceClient(options.ConnectionString);
            }

            return serviceClient.GetBlobContainerClient(options.ContainerName);
        }
    }
}
