namespace Application.Configuration
{
    public sealed class RabbitMqOptions
    {
        public const string SectionName = "RabbitMQ";

        public string HostName { get; set; } = "localhost";
        public int Port { get; set; } = 5672;
        public string VirtualHost { get; set; } = "/";
        public string UserName { get; set; } = "guest";
        public string Password { get; set; } = "guest";
        public string CommandExchangeName { get; set; } = "gameout.driver.commands";
        public string EventExchangeName { get; set; } = "gameout.driver.events";
        public string DriverQueuePrefix { get; set; } = "gameout.driver.commands.";
        public string EventQueueName { get; set; } = "gameout.driver.events.backend";
        public int PrefetchCount { get; set; } = 10;
    }
}