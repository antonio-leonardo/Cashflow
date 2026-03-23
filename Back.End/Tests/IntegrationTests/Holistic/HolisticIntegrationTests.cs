using Cashflow.Shared.Resilience;
using Cashflow.Worker.Report;
using Infrastructure.Test;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using StackExchange.Redis;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Holistic.Integration.Tests
{
    [Collection("CompleteInfrastructureCollection")]
    public class HolisticIntegrationTests : IAsyncLifetime
    {
        private readonly HolisticCompleteInfrastructureFixture _fixture;

        private GatewayWebApplicationFactory _factory = default!;
        private HttpClient _client = default!;

        public HolisticIntegrationTests(HolisticCompleteInfrastructureFixture fixture)
        {
            _fixture = fixture;
            try
            {
                BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
            }
            catch (BsonSerializationException)
            {
                // já registrado por outro fixture, ignorar
            }
        }

        public async Task InitializeAsync()
        {
            _factory = new GatewayWebApplicationFactory(_fixture.KeycloakFixture, _fixture.TransactionApiFixture);
            _client = _factory.CreateClient();
        }

        public async Task DisposeAsync()
        {
            _client.Dispose();
            await _factory.DisposeAsync();
        }

        [Fact]
        public async Task Should_Block_Anonymous_Requests()
        {
            var response = await _client.PostAsJsonAsync(
                "/api/transactions",
                new
                {
                    AccountId = Guid.NewGuid(),
                    Amount = 100m,
                    Currency = "BRL",
                    Type = 1
                });

            Xunit.Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Should_Return_Unauthorized_When_Token_Is_Invalid()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/transactions")
            {
                Content = JsonContent.Create(new
                {
                    AccountId = Guid.NewGuid(),
                    Amount = 100m,
                    Currency = "BRL",
                    Type = 1
                })
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");

            var response = await _client.SendAsync(request);

            Xunit.Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Should_Return_Forbidden_When_Token_Misses_TransactionsWrite_Scope()
        {
            var readOnlyToken = await _fixture.KeycloakFixture.GetReadOnlyAccessTokenClientIdSecretAsync();

            var request = new HttpRequestMessage(HttpMethod.Post, "/api/transactions")
            {
                Content = JsonContent.Create(new
                {
                    AccountId = Guid.NewGuid(),
                    Amount = 150m,
                    Currency = "BRL",
                    Type = 1
                })
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", readOnlyToken);

            var response = await _client.SendAsync(request);

            Xunit.Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Should_Expose_Live_And_Ready_Health_Endpoints()
        {
            var gatewayLive = await _client.GetAsync("/health/live");
            var gatewayReady = await _client.GetAsync("/health/ready");

            Xunit.Assert.Equal(HttpStatusCode.OK, gatewayLive.StatusCode);
            Xunit.Assert.Equal(HttpStatusCode.OK, gatewayReady.StatusCode);

            using var transactionApiClient = new HttpClient
            {
                BaseAddress = _fixture.TransactionApiFixture.BaseAddress,
                Timeout = TimeSpan.FromSeconds(5)
            };

            var apiLive = await transactionApiClient.GetAsync("/health/live");
            var apiReady = await transactionApiClient.GetAsync("/health/ready");

            Xunit.Assert.Equal(HttpStatusCode.OK, apiLive.StatusCode);
            Xunit.Assert.Equal(HttpStatusCode.OK, apiReady.StatusCode);
        }

        [Fact]
        public async Task Should_Recover_Event_Pipeline_After_Outbox_Worker_Restart()
        {
            var token = await _fixture.KeycloakFixture.GetAccessTokenClientIdSecretAsync();
            var accountId = Guid.NewGuid();
            var balanceKey = $"balance:{accountId}";

            await _fixture.OutboxWorkerFixture.StopAsync();
            await Task.Delay(1000);

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "/api/transactions")
                {
                    Content = JsonContent.Create(new
                    {
                        AccountId = accountId,
                        Amount = 310m,
                        Currency = "BRL",
                        Type = 1
                    })
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var createResponse = await _client.SendAsync(request);
                Xunit.Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

                var redis = CreateRedisConnection(_fixture.RedisContainerFixture.ConnectionString);
                var redisDb = redis.GetDatabase();

                var beforeRecovery = await redisDb.StringGetAsync(balanceKey);
                Xunit.Assert.True(beforeRecovery.IsNull);

                await _fixture.OutboxWorkerFixture.StartAsync();
                await Task.Delay(1000);

                var afterRecovery = await WaitForBalanceAsync(redisDb, balanceKey);
                Xunit.Assert.False(afterRecovery.IsNull);
            }
            finally
            {
                await _fixture.OutboxWorkerFixture.StartAsync();
            }
        }

        [Fact]
        public async Task Should_Allow_Authenticated_Requests()
        {
            var token = await _fixture.KeycloakFixture.GetAccessTokenClientIdSecretAsync();

            var objectToRequest = new
            {
                AccountId = Guid.NewGuid(),
                Amount = 200m,
                Currency = "BRL",
                Type = 1
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "/api/transactions")
            {
                Content = JsonContent.Create(objectToRequest)
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await ResiliencePolicies
            .GetHttpResiliencePolicy()
            .ExecuteAsync(() =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "/api/transactions")
                {
                    Content = JsonContent.Create(objectToRequest)
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                return _client.SendAsync(request);
            });

            Xunit.Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            await Task.Delay(10000);

            var mongoClient = CreateMongoDbConnection(_fixture.MongoDbContainerFixture.ConnectionString);
            var redis = CreateRedisConnection(_fixture.RedisContainerFixture.ConnectionString);
            var redisDb = redis.GetDatabase();

            var auditDatabase = mongoClient.GetDatabase("cashflow-audit");
            var reportDatabase = mongoClient.GetDatabase("cashflow-report");

            var auditCollection = auditDatabase.GetCollection<BsonDocument>("events");
            var reportCollection = reportDatabase.GetCollection<TransactionReportDocument>("transactions");
            var balanceValue = await redisDb.StringGetAsync($"balance:{objectToRequest.AccountId}");

            const int RETRIES = 10;

            //----------------------BEGIN: AUDIT----------------------//
            BsonDocument auditResult = null;

            for (int i = 0; i < RETRIES; i++)
            {
                var auditFilter = Builders<BsonDocument>.Filter.Empty;

                var documents = await auditCollection.Find(auditFilter).ToListAsync();

                auditResult = documents.FirstOrDefault(doc =>
                    doc.Contains("Payload") &&
                    doc["Payload"].AsBsonDocument.Contains("AccountId") &&
                    doc["Payload"]["AccountId"].AsGuid == objectToRequest.AccountId
                );

                if (auditResult != null)
                    break;

                await Task.Delay(5000);
            }

            Xunit.Assert.NotNull(auditResult);

            var payload = auditResult["Payload"].AsBsonDocument;

            Xunit.Assert.Equal(objectToRequest.AccountId, payload["AccountId"].AsGuid);
            //----------------------END: AUDIT----------------------//

            //----------------------BEGIN: REPORT----------------------//
            TransactionReportDocument reportResult = null;

            for (int i = 0; i < RETRIES; i++)
            {
                var reportFilter = Builders<TransactionReportDocument>
                    .Filter.Eq(x => x.AccountId, objectToRequest.AccountId);

                reportResult = await reportCollection.Find(reportFilter).FirstOrDefaultAsync();

                if (reportResult != null)
                    break;

                await Task.Delay(5000);
            }

            Xunit.Assert.NotNull(reportResult);
            Xunit.Assert.Equal(objectToRequest.AccountId, reportResult.AccountId);
            Xunit.Assert.Equal(200, reportResult.Amount);
            //----------------------END: REPORT----------------------//

            //----------------------BEGIN: BALANCE----------------------//
            if (balanceValue.IsNull)
            {
                var retries = 10;
                var success = false;

                for (int i = 0; i < retries; i++)
                {
                    balanceValue = await redisDb.StringGetAsync($"balance:{objectToRequest.AccountId}");

                    if (!balanceValue.IsNull)
                    {
                        success = true;
                        break;
                    }

                    await Task.Delay(5000);
                }
            }

            Xunit.Assert.False(balanceValue.IsNull);
            //----------------------END: BALANCE----------------------//
        }

        private ConnectionMultiplexer CreateRedisConnection(string connection)
        {
            var policy = ResiliencePolicies.GetResiliencePolicy();
            return (ConnectionMultiplexer)policy.ExecuteAsync(() =>
            {
                return Task.FromResult<object>(ConnectionMultiplexer.Connect(connection));
            }).GetAwaiter().GetResult();
        }

        private MongoClient CreateMongoDbConnection(string connection)
        {
            var policy = ResiliencePolicies.GetResiliencePolicy();
            return (MongoClient)policy.ExecuteAsync(() =>
            {
                return Task.FromResult<object>(new MongoClient(connection));
            }).GetAwaiter().GetResult();
        }

        private static async Task<RedisValue> WaitForBalanceAsync(IDatabase db, string key, int retries = 30)
        {
            for (int i = 0; i < retries; i++)
            {
                var value = await db.StringGetAsync(key);
                if (!value.IsNull)
                {
                    return value;
                }

                await Task.Delay(2000);
            }

            return RedisValue.Null;
        }
    }
}
