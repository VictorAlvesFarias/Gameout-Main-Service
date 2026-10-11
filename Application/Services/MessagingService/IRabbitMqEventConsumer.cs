using Domain.Queues.AppFileDtos;

namespace Application.Services.MessagingService
{
    public interface IRabbitMqEventConsumer
    {
        Task HandleAsync(DriverEventMessage message);
    }
}