namespace Cashflow.Shared.Events
{
    public abstract class EventBase : IEvent
    {
        protected EventBase(int version = 1)
        {
            EventId = Guid.NewGuid();
            OccurredAt = DateTime.UtcNow;
            Version = version;
            EventType = GetType().Name;
        }

        public Guid EventId { get; }
        public DateTime OccurredAt { get; }
        public string EventType { get; }
        public int Version { get; }
    }
}