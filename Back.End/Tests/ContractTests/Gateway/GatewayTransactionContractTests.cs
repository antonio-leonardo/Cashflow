using PactNet;
using PactNet.Matchers;
using System.Net;
using System.Net.Http.Json;

namespace Gateway.Transaction.ContractTests
{
    public class GatewayTransactionContractTests
    {
        [Fact]
        public async Task Should_Create_Transaction()
        {
            var pact = Pact.V3("Gateway", "TransactionService", new PactConfig());

            var builder = pact.WithHttpInteractions();

            builder
                .UponReceiving("Create Transaction Request")
                .WithRequest(HttpMethod.Post, "/api/transactions")
                .WithJsonBody(new
                {
                    accountId = Match.Type(Guid.NewGuid()),
                    amount = Match.Type(100m),
                    currency = Match.Type("BRL")
                })
                .WillRespond()
                .WithStatus((int)HttpStatusCode.Created);

            await builder.VerifyAsync(async ctx =>
            {
                var client = new HttpClient
                {
                    BaseAddress = ctx.MockServerUri
                };

                var response = await client.PostAsJsonAsync("/api/transactions", new
                {
                    accountId = Guid.NewGuid(),
                    amount = 100m,
                    currency = "BRL"
                });

                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            });
        }
    }
}