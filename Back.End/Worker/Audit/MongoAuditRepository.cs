using Cashflow.Service.Transaction.Domain;
using Cashflow.Shared.NoSql.MongoDB;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Cashflow.Worker.Audit
{
    public class MongoAuditRepository : MongoCommandRepository<AuditDocument>, IAuditRepository
    {
        public MongoAuditRepository(IMongoDatabase database) : base(database, "events")
        {
        }

        public async Task RecordAsync(TransactionCreatedEventV1 evt, CancellationToken cancellationToken = default)
        {
            try
            {
                await InsertAsync(new AuditDocument
                {
                    EventId   = evt.EventId,
                    EventType = evt.EventType,
                    OccurredAt = evt.OccurredAt,
                    Payload   = BuildPayload(evt)
                });
            }
            catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                // Idempotent: duplicate delivery is silently ignored.
            }
        }

        private static BsonDocument BuildPayload(TransactionCreatedEventV1 evt)
        {
            return new BsonDocument
            {
                ["EventId"] = new BsonBinaryData(evt.EventId, GuidRepresentation.Standard),
                ["CorrelationId"] = new BsonBinaryData(evt.CorrelationId, GuidRepresentation.Standard),
                ["OccurredAt"] = evt.OccurredAt,
                ["EventType"] = evt.EventType,
                ["Version"] = evt.Version,
                ["TraceParent"] = BsonValue.Create(evt.TraceParent),
                ["Baggage"] = BsonValue.Create(evt.Baggage),
                ["TransactionId"] = new BsonBinaryData(evt.TransactionId, GuidRepresentation.Standard),
                ["AccountId"] = new BsonBinaryData(evt.AccountId, GuidRepresentation.Standard),
                ["Amount"] = evt.Amount,
                ["Currency"] = evt.Currency,
                ["Type"] = (int)evt.Type
            };
        }
    }
}
