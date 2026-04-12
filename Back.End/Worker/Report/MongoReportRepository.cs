using Cashflow.Service.Transaction.Domain;
using Cashflow.Shared.NoSql.MongoDB;
using MongoDB.Driver;

namespace Cashflow.Worker.Report
{
    public class MongoReportRepository : MongoCommandRepository<TransactionReportDocument>, IReportRepository
    {
        public MongoReportRepository(IMongoDatabase database) : base(database, "transactions")
        {
        }

        public async Task AppendAsync(TransactionCreatedEventV1 evt, CancellationToken cancellationToken = default)
        {
            try
            {
                await InsertAsync(new TransactionReportDocument
                {
                    Id        = evt.TransactionId,
                    AccountId = evt.AccountId,
                    Amount    = evt.Amount,
                    Currency  = evt.Currency,
                    CreatedAt = evt.OccurredAt
                });
            }
            catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                // Idempotent: duplicate delivery is silently ignored.
            }
        }
    }
}
