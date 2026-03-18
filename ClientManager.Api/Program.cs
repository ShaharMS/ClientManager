using System.Text.Json.Serialization;
using ClientManager.Api.Extensions;
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

    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

    builder.Services.AddOpenApi();

    // Application services, persistence, rate limiting, etc.
    builder.Services.AddClientManager(builder.Configuration);

    // OpenTelemetry metrics + Prometheus
    builder.Services.AddSingleton<ClientManagerMetrics>();
    builder.Services.AddOpenTelemetry()
        .WithMetrics(metrics =>
        {
            metrics.AddAspNetCoreInstrumentation();
            metrics.AddMeter(ClientManagerMetrics.MeterName);
            metrics.AddPrometheusExporter();
        });

    var app = builder.Build();

    // Middleware pipeline — order matters
    app.UseMiddleware<RequestTrackingMiddleware>();
    app.UseMiddleware<ErrorHandlingMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseHttpsRedirection();
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
