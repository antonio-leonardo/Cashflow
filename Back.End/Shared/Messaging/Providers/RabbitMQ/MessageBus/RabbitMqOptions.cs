namespace Cashflow.Shared.Messaging.RabbitMQ.MessageBus
{
    public class RabbitMqOptions
    {
        public string Host { get; set; } = "localhost";

        public int Port { get; set; } = 5672;

        public string Username { get; set; } = "guest";

        public string Password { get; set; } = "guest";

        public int RetryCount { get; set; } = 5;

        public int RetryDelaySeconds { get; set; } = 5;

        public string ConsumerName { get; set; } = string.Empty;
    }
}
