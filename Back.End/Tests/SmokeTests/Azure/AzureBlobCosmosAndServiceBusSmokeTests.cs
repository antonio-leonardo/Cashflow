using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Cashflow.Service.Transaction.Domain;
using Cashflow.Shared.Messaging.Abstractions;
using Cashflow.Shared.Messaging.AzureServiceBus.MessageBus;
using Cashflow.Shared.Storage.Abstractions;
using Cashflow.Shared.Storage.AzureBlob;
using Cashflow.Worker.Report;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Text;

namespace Azure.Smoke.Tests
{
    [Trait("Category", "AzureSmoke")]
    public class AzureBlobCosmosAndServiceBusSmokeTests
    {
        [Fact]
        public async Task Should_Upload_Report_To_Real_Azure_Blob_Storage()
        {
            var connectionString = AzureSmokeSettings.GetOptional("AZURE_SMOKE_BLOB_CONNECTION_STRING");
            var containerName = AzureSmokeSettings.GetOptional("AZURE_SMOKE_BLOB_CONTAINER");
            if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(containerName))
            {
                return;
            }

            var blobServiceClient = new BlobServiceClient(connectionString);
            await blobServiceClient.GetBlobContainerClient(containerName).CreateIfNotExistsAsync();

            var store = new AzureBlobReportArtifactStore(new AzureBlobStorageOptions
            {
                ConnectionString = connectionString,
                ContainerName = containerName
            });

            var path = $"smoke/{Guid.NewGuid():N}/report.csv";
            await using var upload = new MemoryStream(Encoding.UTF8.GetBytes("id,amount\n1,100"));
            await store.UploadAsync(path, upload, "text/csv");

            Assert.True(await store.ExistsAsync(path));
            var downloadUri = await store.GetDownloadUriAsync(path, TimeSpan.FromMinutes(15));
            Assert.True(downloadUri.IsAbsoluteUri);
        }

        [Fact]
        public async Task Should_Export_Daily_Report_From_Real_Cosmos_Mongo_Api()
        {
            var connectionString = AzureSmokeSettings.GetOptional("AZURE_SMOKE_COSMOS_MONGODB_CONNECTION");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return;
            }

            var databaseName = $"cashflow-smoke-{Guid.NewGuid():N}";
            var client = new MongoClient(connectionString);
            var database = client.GetDatabase(databaseName);
            var collection = database.GetCollection<TransactionReportDocument>("transactions");

            var accountId = Guid.NewGuid();
            var date = DateOnly.FromDateTime(DateTime.UtcNow);
            var createdAt = date.ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromHours(12)), DateTimeKind.Utc);

            await collection.InsertOneAsync(new TransactionReportDocument
            {
                Id = Guid.NewGuid(),
                AccountId = accountId,
                Amount = 42.5m,
                Currency = "BRL",
                CreatedAt = createdAt
            });

            var store = new InMemoryReportArtifactStore();
            var service = new ReportExportService(database, store);
            var result = await service.ExportDailyAsync(accountId, date);

            Assert.Equal(1, result.TransactionCount);
            Assert.True(store.StoredContent.Contains("42.5", StringComparison.Ordinal));
        }

        [Fact]
        public async Task Should_Publish_And_Consume_Message_From_Real_Azure_Service_Bus()
        {
            var connectionString = AzureSmokeSettings.GetOptional("AZURE_SMOKE_SERVICEBUS_CONNECTION_STRING");
            var subscriptionName = AzureSmokeSettings.GetOptional("AZURE_SMOKE_SERVICEBUS_SUBSCRIPTION");
            if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(subscriptionName))
            {
                return;
            }

            await using var publisher = CreateBus(connectionString, string.Empty);
            await using var consumer = CreateBus(connectionString, subscriptionName);

            var received = new TaskCompletionSource<EventEnvelope<TransactionCreatedEventV1>>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));

            await consumer.SubscribeAsync<TransactionCreatedEventV1>((envelope, _) =>
            {
                received.TrySetResult(envelope);
                return Task.CompletedTask;
            }, cts.Token);

            await Task.Delay(500, cts.Token);

            var evt = new TransactionCreatedEventV1(
                Guid.NewGuid(),
                Guid.NewGuid(),
                99m,
                "BRL",
                TransactionType.Credit);

            var envelope = new EventEnvelope<TransactionCreatedEventV1>(
                evt,
                new MessageMetadata(
                    CorrelationId: Guid.NewGuid().ToString(),
                    CausationId: evt.EventId.ToString(),
                    Source: nameof(AzureBlobCosmosAndServiceBusSmokeTests),
                    TenantId: null,
                    CreatedAtUtc: DateTime.UtcNow,
                    SessionId: Guid.NewGuid().ToString()));

            await publisher.PublishAsync(envelope, cts.Token);

            var consumed = await received.Task.WaitAsync(cts.Token);
            Assert.Equal(evt.TransactionId, consumed.Event.TransactionId);
        }

        private static AzureServiceBusBus CreateBus(string connectionString, string consumerName)
        {
            return new AzureServiceBusBus(Options.Create(new AzureServiceBusOptions
            {
                ConnectionString = connectionString,
                ConsumerName = consumerName,
                MaxConcurrentCalls = 1,
                MaxAutoLockRenewalSeconds = 60
            }));
        }

        private sealed class InMemoryReportArtifactStore : IReportArtifactStore
        {
            public string StoredContent { get; private set; } = string.Empty;

            public Task<string> UploadAsync(string path, Stream content, string contentType, CancellationToken cancellationToken = default)
            {
                using var reader = new StreamReader(content, Encoding.UTF8, leaveOpen: true);
                StoredContent = reader.ReadToEnd();
                return Task.FromResult(path);
            }

            public Task<Stream> DownloadAsync(string path, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(StoredContent)));
            }

            public Task<Uri> GetDownloadUriAsync(string path, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new Uri($"https://smoke.local/{path}"));
            }

            public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(!string.IsNullOrWhiteSpace(StoredContent));
            }
        }
    }
}
