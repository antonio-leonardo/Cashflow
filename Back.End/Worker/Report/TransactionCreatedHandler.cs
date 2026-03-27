using Cashflow.Service.Transaction.Domain;
using Cashflow.Shared.NoSql.MongoDB;
using MongoDB.Driver;

namespace Cashflow.Worker.Report
{
    public class TransactionCreatedHandler : MongoCommandRepository<TransactionReportDocument>
    {
        public TransactionCreatedHandler(IMongoDatabase database) : base(database, "transactions")
        {
        }

        public async Task HandleAsync(TransactionCreatedEventV1 evt, CancellationToken ct)
        {
            try
            {
                await this.InsertAsync(new TransactionReportDocument
                {
                    Id = evt.TransactionId,
                    AccountId = evt.AccountId,
                    Amount = evt.Amount,
                    Currency = evt.Currency,
                    CreatedAt = evt.OccurredAt
                });
            }
            catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                // Idempotent consumer: duplicate delivery of the same event is ignored.
            }
        }
    }
}