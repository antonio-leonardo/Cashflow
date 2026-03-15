namespace Cashflow.Back.End.Shared.Events
{
    public abstract class EventBase : IEvent
    {
        protected EventBase(Guid? correlationId = null, int version = 1)
        {
            EventId = Guid.NewGuid();
            CorrelationId = correlationId ?? Guid.NewGuid();
            OccurredAt = DateTime.UtcNow;
            Version = version;
            EventType = GetType().Name;
        }

        public Guid EventId { get; }
        public Guid CorrelationId { get; }
        public DateTime OccurredAt { get; }
        public string EventType { get; }
        public int Version { get; }
    }
}