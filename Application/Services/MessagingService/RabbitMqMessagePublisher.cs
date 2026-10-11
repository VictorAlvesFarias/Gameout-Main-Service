using System.Text;
using System.Text.Json;
using Application.Configuration;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Application.Services.MessagingService
{
    public class RabbitMqMessagePublisher : IRabbitMqMessagePublisher
    {
        private readonly RabbitMqOptions _options;

        public RabbitMqMessagePublisher(IOptions<RabbitMqOptions> options)
        {
            _options = options.Value;
        }

        public Task PublishAsync<T>(string exchangeName, string routingKey, T message, IDictionary<string, object>? headers = null)
        {
            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                VirtualHost = _options.VirtualHost,
                UserName = _options.UserName,
                Password = _options.Password,
                DispatchConsumersAsync = true,
                AutomaticRecoveryEnabled = true
            };

            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            channel.ExchangeDeclare(exchangeName, ExchangeType.Direct, durable: true, autoDelete: false);

            var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";
            properties.Headers = headers;

            channel.BasicPublish(exchangeName, routingKey, properties, payload);

            return Task.CompletedTask;
        }
    }
}