using System;
using System.Collections.Generic;
using System.Text;

namespace Cashflow.Shared.Messaging.Providers.RabbitMQ
{
    public class RabbitMqOptions
    {
        public string Host { get; set; } = "localhost";

        public int Port { get; set; } = 5672;

        public string Username { get; set; } = "guest";

        public string Password { get; set; } = "guest";
    }
}
