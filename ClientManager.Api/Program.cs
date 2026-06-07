using System.Reflection;
using System.Text.Json.Serialization;

using Asp.Versioning;

using ClientManager.Api.Middlewares;
using ClientManager.Api.Models.Configuration;
using ClientManager.Api.Services.Storage;
using ClientManager.Api.Services.Storage.Instrumentation;
using ClientManager.Api.Utils.Extensions;
using ClientManager.Api.Utils.Instrumentation;
using ClientManager.Api.Utils.Swagger;
using ClientManager.Shared.Logging;
using ClientManager.Shared.Models.Problems;

using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

using NLog;
using NLog.Web;

using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

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

    builder.Services.AddSingleton<IValidateOptions<ApiVersioningSettings>, ApiVersioningSettingsValidator>();
    builder.Services.AddOptions<ApiVersioningSettings>()
        .Bind(builder.Configuration.GetSection(ApiVersioningSettings.SectionName))
        .ValidateOnStart();

    var apiVersioningSettings = builder.Configuration
        .GetSection(ApiVersioningSettings.SectionName)
        .Get<ApiVersioningSettings>() ?? new ApiVersioningSettings();
    var defaultVersion = ApiVersionParser.Default.Parse(apiVersioningSettings.DefaultVersion);

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

        // Load XML docs from the shared assembly so request/response/entity schemas render
        // their authored descriptions in Swagger alongside the API's own operation docs.
        var sharedXmlFile = $"{typeof(ProblemResponse).Assembly.GetName().Name}.xml";
        var sharedXmlPath = Path.Combine(AppContext.BaseDirectory, sharedXmlFile);
        if (File.Exists(sharedXmlPath))
        {
            options.IncludeXmlComments(sharedXmlPath);
        }

        options.DocumentFilter<TagDescriptionsDocumentFilter>();
    });

    builder.Services.AddPublicApiServices();

    // In-process storage domain services.
    builder.Services.AddInProcessStorageServices(builder.Configuration, builder.Environment);

    // OpenTelemetry metrics, traces, and Prometheus
    builder.Services.AddSingleton<ClientManagerMetrics>();
    builder.Services.AddSingleton<IValidateOptions<ObservabilityOptions>, ObservabilityOptionsValidator>();
    builder.Services.AddOptions<ObservabilityOptions>()
        .Bind(builder.Configuration.GetSection(ObservabilityOptions.SectionName))
        .ValidateOnStart();

    var observabilityOptions = builder.Configuration
        .GetSection(ObservabilityOptions.SectionName)
        .Get<ObservabilityOptions>() ?? new ObservabilityOptions();
    var otlpEndpoint = observabilityOptions.OtlpEndpoint;
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService("ClientManager.Api"))
        .WithMetrics(metrics =>
        {
            metrics.AddAspNetCoreInstrumentation();
            metrics.AddMeter(ClientManagerMetrics.MeterName);
            metrics.AddMeter(StorageMetrics.MeterName);
            metrics.AddPrometheusExporter();
        })
        .WithTracing(tracing =>
        {
            tracing.AddAspNetCoreInstrumentation();
            tracing.AddHttpClientInstrumentation();
            tracing.AddSource(ClientManagerMetrics.ActivitySourceName);
            tracing.AddSource(StorageMetrics.ActivitySourceName);

            if (Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out var endpoint))
            {
                tracing.AddOtlpExporter(options => options.Endpoint = endpoint);
            }
        });

    var app = builder.Build();

    // Middleware pipeline - order matters
    app.UseMiddleware<RequestTrackingMiddleware>();
    app.UseMiddleware<ErrorHandlingMiddleware>();

    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "ClientManager API v1");
        options.RoutePrefix = "docs";
    });

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
            appLogger.Info("Swagger docs available", new { DocsUrl = $"{url}/docs" });
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
