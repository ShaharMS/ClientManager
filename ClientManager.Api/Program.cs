using System.Reflection;
using System.Text.Json.Serialization;
using Asp.Versioning;
using ClientManager.Api.Extensions;
using ClientManager.Api.Middleware;
using ClientManager.Api.Services.Instrumentation;
using ClientManager.Api.Swagger;
using ClientManager.Shared.Logging;
using Microsoft.OpenApi;
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

    var versionConfig = builder.Configuration.GetSection("ApiVersioning");
    var defaultVersion = ApiVersionParser.Default.Parse(versionConfig["DefaultVersion"] ?? "1.0");

    builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = defaultVersion;
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddApiExplorer(options =>
    {
        options.SubstituteApiVersionInUrl = true;
        options.GroupNameFormat = "'v'VVV";
    });

    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "ClientManager API",
            Version = "v1"
        });

        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        options.IncludeXmlComments(xmlPath);
        options.DocumentFilter<TagDescriptionsDocumentFilter>();
    });

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

    // Middleware pipeline - order matters
    app.UseMiddleware<RequestTrackingMiddleware>();
    app.UseMiddleware<ErrorHandlingMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "ClientManager API v1");
            options.RoutePrefix = "docs";
        });
    }

    app.UseHttpsRedirection();
    app.UseAuthorization();
    app.MapControllers();
    app.MapPrometheusScrapingEndpoint("/prometheus/otel");

    app.Lifetime.ApplicationStarted.Register(() =>
    {
        var appLogger = app.Services.GetRequiredService<IAppLogger<Program>>();
        var urls = app.Urls;
        foreach (var url in urls)
        {
            appLogger.Info("API listening", new { Url = url });
            if (app.Environment.IsDevelopment())
            {
                appLogger.Info("Swagger docs available", new { DocsUrl = $"{url}/docs" });
            }
        }
    });

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
