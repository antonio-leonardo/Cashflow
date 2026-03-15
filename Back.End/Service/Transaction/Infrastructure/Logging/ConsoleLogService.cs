using Cashflow.Back.End.Shared.Logging;

namespace Cashflow.Back.End.Service.Transaction.Infrastructure.Logging;

public sealed class ConsoleLogService : ILogService
{
    public void Log(
        LogLevel level,
        string message,
        LogContext context,
        Exception? exception = null,
        IReadOnlyDictionary<string, object>? additionalData = null)
    {
        var parts = new List<string>
        {
            $"[{level}]",
            context.ServiceName,
            context.CorrelationId ?? "-",
            context.TransactionId ?? "-",
            context.UserId ?? "-",
            message
        };
        if (exception is not null)
            parts.Add(exception.ToString());
        if (additionalData is { Count: > 0 })
            parts.Add(string.Join(", ", additionalData.Select(kv => $"{kv.Key}={kv.Value}")));

        var line = string.Join(" | ", parts);
        Console.WriteLine(line);
    }
}