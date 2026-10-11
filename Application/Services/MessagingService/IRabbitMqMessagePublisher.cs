namespace Application.Services.MessagingService
{
    public interface IRabbitMqMessagePublisher
    {
        Task PublishAsync<T>(string exchangeName, string routingKey, T message, IDictionary<string, object>? headers = null);
    }
}