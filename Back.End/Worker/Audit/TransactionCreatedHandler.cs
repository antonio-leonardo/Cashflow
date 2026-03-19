using Cashflow.Service.Transaction.Domain;
using Cashflow.Shared.NoSql.MongoDB;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Cashflow.Worker.Audit
{
    public class TransactionCreatedHandler : MongoCommandRepository<AuditDocument>
    {
        public TransactionCreatedHandler(IMongoDatabase database) : base(database, "events")
        {
        }

        public async Task HandleAsync(TransactionCreatedEventV1 evt, CancellationToken ct)
        {
            await this.InsertAsync(new AuditDocument
            {
                EventId = evt.EventId,
                EventType = evt.EventType,
                OccurredAt = evt.OccurredAt,
                Payload = @evt.ToBsonDocument()
            });
        }
    }
}