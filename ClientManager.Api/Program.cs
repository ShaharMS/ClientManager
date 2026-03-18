using System.Text.Json.Serialization;
using ClientManager.Api.Middleware;
using ClientManager.Api.Services.Instrumentation;
using NLog;
using NLog.Web;
using OpenTelemetry.Metrics;

var logger = LogManager.Setup()
    .LoadConfigurationFromAppSettings()
    .GetCurrentClassLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

    // Add services to the container.

    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
    // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
    builder.Services.AddOpenApi();

    builder.Services.AddSingleton<ClientManagerMetrics>();

    builder.Services.AddOpenTelemetry()
        .WithMetrics(metrics =>
        {
            metrics.AddAspNetCoreInstrumentation();
            metrics.AddMeter(ClientManagerMetrics.MeterName);
            metrics.AddPrometheusExporter();
        });

    var app = builder.Build();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseHttpsRedirection();

    app.UseMiddleware<RequestTrackingMiddleware>();
    app.UseMiddleware<ErrorHandlingMiddleware>();

    app.UseAuthorization();

    app.MapControllers();

    app.MapPrometheusScrapingEndpoint("/metrics");

    app.Run();
}
catch (Exception exception)
{
    logger.Error(exception, "Stopped program because of exception");
    throw;
}
finally
{
    LogManager.Shutdown();
}
