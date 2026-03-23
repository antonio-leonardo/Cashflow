using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Cashflow.Worker.Audit
{
    public class AuditDocument
    {
        [BsonRepresentation(BsonType.String)]
        public Guid EventId { get; set; }

        public string EventType { get; set; } = string.Empty;

        public DateTime OccurredAt { get; set; }

        public BsonDocument Payload { get; set; } = new();
    }
}
