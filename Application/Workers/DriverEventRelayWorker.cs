using System.Text.Json;
using Application.Configuration;
using Application.Services.MessagingService;
using Domain.Queues.AppFileDtos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Application.Workers
{
    public class DriverEventRelayWorker : BackgroundService
    {
        private readonly RabbitMqOptions _options;
        private readonly IServiceScopeFactory _scopeFactory;

        public DriverEventRelayWorker(IOptions<RabbitMqOptions> options, IServiceScopeFactory scopeFactory)
        {
            _options = options.Value;
            _scopeFactory = scopeFactory;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
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

            var connection = factory.CreateConnection();
            var channel = connection.CreateModel();
            channel.ExchangeDeclare(_options.EventExchangeName, ExchangeType.Direct, durable: true, autoDelete: false);
            channel.QueueDeclare(_options.EventQueueName, durable: true, exclusive: false, autoDelete: false);
            channel.QueueBind(_options.EventQueueName, _options.EventExchangeName, _options.EventQueueName);
            channel.BasicQos(0, (ushort)Math.Max(1, _options.PrefetchCount), false);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.Received += async (_, args) =>
            {
                try
                {
                    var payload = JsonSerializer.Deserialize<DriverEventMessage>(args.Body.ToArray(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (payload != null)
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var handler = scope.ServiceProvider.GetRequiredService<IRabbitMqEventConsumer>();
                        await handler.HandleAsync(payload);
                    }

                    channel.BasicAck(args.DeliveryTag, false);
                }
                catch
                {
                    channel.BasicNack(args.DeliveryTag, false, requeue: true);
                }
            };

            channel.BasicConsume(_options.EventQueueName, autoAck: false, consumer);

            return Task.Delay(Timeout.Infinite, stoppingToken);
        }
    }
}