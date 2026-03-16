using Cashflow.Back.End.Service.Transaction.Domain;
using Cashflow.Shared.NoSql.Providers.MongoDB;
using MongoDB.Driver;

namespace Cashflow.Back.End.Worker.Audit
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
                Payload = evt
            });
        }
    }
}