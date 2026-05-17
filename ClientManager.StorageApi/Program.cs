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
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var logger = LogManager.Setup()
    .LoadConfigurationFromAppSettings()
    .GetCurrentClassLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    ApplyDevelopmentDefaults(builder);

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

    builder.Services.AddStorageApi(builder.Configuration, builder.Environment);

    builder.Services.AddSingleton<StorageApiMetrics>();
    var otlpEndpoint = builder.Configuration["Observability:OtlpEndpoint"];
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService("ClientManager.StorageApi"))
        .WithMetrics(metrics =>
        {
            metrics.AddAspNetCoreInstrumentation();
            metrics.AddMeter(StorageApiMetrics.MeterName);
            metrics.AddPrometheusExporter();
        })
        .WithTracing(tracing =>
        {
            tracing.AddAspNetCoreInstrumentation();
            tracing.AddHttpClientInstrumentation();
            tracing.AddSource("ClientManager.Api");
            tracing.AddSource(StorageApiMetrics.ActivitySourceName);

            if (Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out var endpoint))
            {
                tracing.AddOtlpExporter(options => options.Endpoint = endpoint);
            }
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

static void ApplyDevelopmentDefaults(WebApplicationBuilder builder)
{
    if (!builder.Environment.IsDevelopment())
    {
        return;
    }

    var defaults = new Dictionary<string, string?>();

    if (ShouldUseRepositoryDataDirectory(builder.Configuration["Persistence:DefaultJsonFile:DataDirectory"]))
    {
        defaults["Persistence:DefaultJsonFile:DataDirectory"] = ResolveRepositoryDataDirectory();
    }

    if (defaults.Count > 0)
    {
        builder.Configuration.AddInMemoryCollection(defaults);
    }
}

static bool ShouldUseRepositoryDataDirectory(string? configuredDataDirectory)
{
    if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("Persistence__DefaultJsonFile__DataDirectory")))
    {
        return false;
    }

    return string.IsNullOrWhiteSpace(configuredDataDirectory)
        || configuredDataDirectory is "./data" or ".\\data" or "data";
}

static string ResolveRepositoryDataDirectory()
{
    var current = new DirectoryInfo(AppContext.BaseDirectory);
    while (current is not null)
    {
        var solutionFile = Path.Combine(current.FullName, "ClientManager.slnx");
        var dataDirectory = Path.Combine(current.FullName, "data");
        if (File.Exists(solutionFile) && Directory.Exists(dataDirectory))
        {
            return dataDirectory;
        }

        current = current.Parent;
    }

    return Path.GetFullPath("./data");
}