using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Cashflow.Worker.Report
{
    public class TransactionReportDocument
    {
        [BsonRepresentation(BsonType.String)]
        public Guid Id { get; set; }

        public Guid AccountId { get; set; }

        public decimal Amount { get; set; }

        public string Currency { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
    }
}
