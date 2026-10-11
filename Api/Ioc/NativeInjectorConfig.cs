using Application.Configuration;
using Application.Services.ApiKeyService;
using Application.Services.ApplicationLogService;
using Application.Services.AppFileService;
using Application.Services.DownloadSignatureService;
using Application.Services.Identity;
using Application.Services.IdentityService;
using Application.Services.MessagingService;
using Application.Services.StorageService;
using Application.Services.WebSocketService;
using Application.Workers;
using Domain.Entitites.ApplicationContextDb;
using Domain.Queues.AppFileDtos;
using Infrastructure.Context;
using Infrastructure.Factories;
using Infrastructure.Mediators;
using Microsoft.AspNetCore.Identity;
using Web.Api.Toolkit.Entity.Infraestructure.Factories;
using Web.Api.Toolkit.Entity.Infraestructure.Mediators;
using Web.Api.Toolkit.Entity.Infraestructure.Repositories;
using Web.Api.Toolkit.Identity.Application.Extensions;
using Web.Api.Toolkit.Queues.Application.Services;

namespace ASP.NET_Core_Template.Ioc
{
    public static class NativeInjectorConfig
    {
        public static void RegisterServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Registrar fila de processamento
            services.AddSingleton(typeof(IQueueService<>), typeof(QueueService<>));
            
            services.AddSingleton<ApplicationContext>();
            services.AddSingleton<AppFileWorker>();

            services.AddHostedService(provider => provider.GetRequiredService<AppFileWorker>());

            services.AddDefaultIdentity<ApplicationUser, IdentityRole, ApplicationDbContext>();

            services.AddScoped<IDatabaseContextFactory, DbContextFactory>();
            services.AddScoped(typeof(IBaseRepository<>), typeof(BaseRepository<>));
            services.AddScoped(typeof(IDatabaseContextMediator<>), typeof(UserOwnedDatabaseMediator<>));
            services.AddScoped(typeof(IDatabaseContextMediator<>), typeof(SoftDeleteMediator<>));
            services.AddScoped<IAppFileService, AppFileService>();
            services.AddScoped<IApplicationLogService, ApplicationLogService>();
            services.AddScoped<IIdentityService, IdentityService>();
            services.AddScoped<IApiKeyService, ApiKeyService>();
            services.AddScoped<IWebSocketService, WebSocketService>();
            services.AddScoped<IRabbitMqEventConsumer, RabbitMqEventConsumer>();
            services.AddSingleton<IRabbitMqMessagePublisher, RabbitMqMessagePublisher>();
            services.AddSingleton<IMinioStorageService, MinioStorageService>();
            services.AddScoped<IDownloadSignatureService, DownloadSignatureService>();

            services.Configure<DriverApiKeyOptions>(configuration.GetSection(DriverApiKeyOptions.SectionName));
            services.Configure<DownloadOptions>(configuration.GetSection(DownloadOptions.SectionName));
            services.Configure<Application.Configuration.WebSocketOptions>(configuration.GetSection(Application.Configuration.WebSocketOptions.SectionName));
            services.Configure<MinioOptions>(configuration.GetSection(MinioOptions.SectionName));
            services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));

            services.AddHostedService<DriverEventRelayWorker>();
        }
    }
}
