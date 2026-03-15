namespace Cashflow.Back.End.Shared.Logging
{
    public interface ILogService
    {
        void Log(
            LogLevel level,
            string message,
            LogContext context,
            Exception? exception = null,
            IReadOnlyDictionary<string, object>? additionalData = null);
    }
}