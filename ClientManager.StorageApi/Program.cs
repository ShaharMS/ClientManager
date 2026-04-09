using System.Reflection;
using System.Text.Json.Serialization;
using Asp.Versioning;
using ClientManager.Shared.Logging;
using ClientManager.StorageApi.Middlewares;
using ClientManager.StorageApi.Utils.Extensions;
using ClientManager.StorageApi.Utils.Instrumentation;
using ClientManager.StorageApi.Utils.Swagger;
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
            Title = "ClientManager Storage API",
            Version = "v1"
        });

        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        options.IncludeXmlComments(xmlPath);
        options.DocumentFilter<TagDescriptionsDocumentFilter>();
    });

    builder.Services.AddStorageApi(builder.Configuration);

    builder.Services.AddSingleton<StorageApiMetrics>();
    builder.Services.AddOpenTelemetry()
        .WithMetrics(metrics =>
        {
            metrics.AddAspNetCoreInstrumentation();
            metrics.AddMeter(StorageApiMetrics.MeterName);
            metrics.AddPrometheusExporter();
        });

    var app = builder.Build();

    app.UseMiddleware<RequestTrackingMiddleware>();
    app.UseMiddleware<ErrorHandlingMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "ClientManager Storage API v1");
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
        foreach (var url in app.Urls)
        {
            appLogger.Info("Storage API listening", new { Url = url });
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
    logger.Error(exception, "Stopped storage API because of exception");
    throw;
}
finally
{
    LogManager.Shutdown();
}