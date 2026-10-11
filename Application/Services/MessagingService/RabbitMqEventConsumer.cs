using Application.Services.ApplicationLogService;
using Application.Services.AppFileService;
using Application.Types;
using Domain.Queues.AppFileDtos;

namespace Application.Services.MessagingService
{
    public class RabbitMqEventConsumer : IRabbitMqEventConsumer
    {
        private readonly IAppFileService _appFileService;
        private readonly IApplicationLogService _applicationLogService;

        public RabbitMqEventConsumer(IAppFileService appFileService, IApplicationLogService applicationLogService)
        {
            _appFileService = appFileService;
            _applicationLogService = applicationLogService;
        }

        public async Task HandleAsync(DriverEventMessage message)
        {
            if (message == null)
            {
                return;
            }

            if (string.Equals(message.Event, DriverEventTypes.Heartbeat, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (message.AppFileId > 0)
            {
                await _applicationLogService.AddLogAsync(
                    0,
                    $"Driver event received: {message.Event}",
                    ApplicationLogType.Message,
                    ApplicationLogAction.Info,
                    message.Details ?? message.Message
                );
            }

            if (string.Equals(message.Event, DriverEventTypes.Completed, StringComparison.OrdinalIgnoreCase))
            {
                await _appFileService.SetAppFileStatus(message.AppFileId, AppFileStatusTypes.Synced);
                return;
            }

            if (string.Equals(message.Event, DriverEventTypes.Error, StringComparison.OrdinalIgnoreCase))
            {
                await _appFileService.SetAppFileStatus(message.AppFileId, AppFileStatusTypes.Unsynced);
            }
        }
    }
}