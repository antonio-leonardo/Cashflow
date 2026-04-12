using System.Diagnostics;

namespace Cashflow.Shared.Events
{
    public abstract class EventBase : IEvent
    {
        protected EventBase(
            Guid? eventId = null,
            Guid? correlationId = null,
            DateTime? occurredAt = null,
            int version = 1,
            string? eventType = null,
            string? traceParent = null,
            string? baggage = null)
        {
            EventId = eventId ?? Guid.NewGuid();
            CorrelationId = correlationId ?? Guid.NewGuid();
            OccurredAt = occurredAt ?? DateTime.UtcNow;
            Version = version <= 0 ? 1 : version;
            EventType = string.IsNullOrWhiteSpace(eventType) ? GetType().Name : eventType;
            TraceParent = traceParent ?? Activity.Current?.Id;
            Baggage = baggage ?? BuildBaggageHeaderFromActivity();
        }

        public Guid EventId { get; }

        public Guid CorrelationId { get; }

        public DateTime OccurredAt { get; }
        public string EventType { get; }
        public int Version { get; }
        public string? TraceParent { get; }
        public string? Baggage { get; }

        private static string? BuildBaggageHeaderFromActivity()
        {
            var activity = Activity.Current;
            if (activity is null)
            {
                return null;
            }

            var entries = new List<string>();
            foreach (var pair in activity.Baggage)
            {
                if (!string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
                {
                    entries.Add($"{pair.Key}={pair.Value}");
                }
            }

            return entries.Count == 0 ? null : string.Join(",", entries);
        }
    }
}
