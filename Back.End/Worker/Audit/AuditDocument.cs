namespace Cashflow.Worker.Audit
{
    public class AuditDocument
    {
        public Guid EventId { get; set; }

        public string EventType { get; set; }

        public DateTime OccurredAt { get; set; }

        public object Payload { get; set; }
    }
}