namespace Cashflow.Back.End.Shared.Events
{
    public interface IEvent
    {
        Guid EventId { get; }
        DateTime OccurredAt { get; }
        string EventType { get; }
        int Version { get; }
    }
}