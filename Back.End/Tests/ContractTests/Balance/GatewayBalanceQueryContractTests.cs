using PactNet;
using PactNet.Matchers;
using System.Net;
using System.Net.Http.Json;

namespace Balance.Query.ContractTests
{
    public class GatewayBalanceQueryContractTests
    {
        [Fact]
        public async Task Should_Get_Daily_Balance_By_Account_And_Date()
        {
            var accountId = Guid.NewGuid();
            const string referenceDate = "2026-03-26";

            var pact = Pact.V3("Gateway", "BalanceQueryService", new PactConfig());
            var builder = pact.WithHttpInteractions();

            builder
                .UponReceiving("Daily Balance Query")
                .WithRequest(HttpMethod.Get, $"/api/v1/balance/daily/{accountId}")
                .WithQuery("date", Match.Regex(referenceDate, "\\d{4}-\\d{2}-\\d{2}"))
                .WillRespond()
                .WithStatus((int)HttpStatusCode.OK)
                .WithJsonBody(new
                {
                    accountId = Match.Type(accountId),
                    date = Match.Regex(referenceDate, "\\d{4}-\\d{2}-\\d{2}"),
                    totalCredits = Match.Type(1200.50m),
                    totalDebits = Match.Type(200.10m),
                    netBalance = Match.Type(1000.40m)
                });

            await builder.VerifyAsync(async ctx =>
            {
                var client = new HttpClient
                {
                    BaseAddress = ctx.MockServerUri
                };

                var response = await client.GetAsync(
                    $"/api/v1/balance/daily/{accountId}?date={referenceDate}");

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                var body = await response.Content.ReadFromJsonAsync<DailyBalanceContractResponse>();
                var payload = Assert.IsType<DailyBalanceContractResponse>(body);

                Assert.Equal(accountId, payload.AccountId);
                Assert.Equal(referenceDate, payload.Date);
                Assert.Equal(1200.50m, payload.TotalCredits);
                Assert.Equal(200.10m, payload.TotalDebits);
                Assert.Equal(1000.40m, payload.NetBalance);
            });
        }

        private sealed record DailyBalanceContractResponse(
            Guid AccountId,
            string Date,
            decimal TotalCredits,
            decimal TotalDebits,
            decimal NetBalance);
    }
}
