using Application.Workers;
using ASP.NET_Core_Template.Ioc;
using ASP.NET_Core_Template.Setups;
using Infrastructure.Context;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Web.Api.Toolkit.Ws.Application.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Configurações para arquivos sem limites de tamanho
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = null; // Sem limite
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = long.MaxValue; // Sem limite
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartHeadersLengthLimit = int.MaxValue;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: "AllowedCorsOrigins",
        builder =>
        {
            builder
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

builder.Services.AddControllers();

if (builder.Configuration.GetValue<bool>("Swagger:Enabled", false))
{
    builder.Services.AddEndpointsApiExplorer();

    builder.Services.AddSwaggerGen();

    builder.Services.AddSwagger();
}

builder.Services.RegisterServices(builder.Configuration);

builder.Services.AddDbContext<ApplicationDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
);

builder.Services.AddIdentityAuthentication(builder.Configuration);

builder.Services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, ASP.NET_Core_Template.Middleware.ApiKeyAuthenticationHandler>(
    "ApiKey", options => { }
);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowedCorsOrigins",
    builder => builder
        .AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader()
    );
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.Migrate();
}

if (builder.Configuration.GetValue<bool>("Swagger:Enabled", false))
{
    app.UseSwagger();

    app.UseSwaggerUI();
}

app.UseCors("AllowedCorsOrigins");

app.UseHttpsRedirection();

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});

app.UseAuthentication();

app.UseAuthorization();

app.UseWebSocketEndpoint<AppFileWorker>();

app.MapControllers();

app.Run();