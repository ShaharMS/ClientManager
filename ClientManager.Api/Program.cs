using System.Reflection;
using System.Text.Json.Serialization;
using ClientManager.Api.Filters;
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

var logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Logging.ClearProviders();
    builder.Host.UseNLog();
    builder.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
    builder.Services.AddScoped<SeedEndpointGateFilter>();
    builder.Services.AddPublicApiServices();
    builder.Services.AddInProcessStorageServices(builder.Configuration, builder.Environment);
    builder.Services.AddSingleton<ClientManagerMetrics>();
    builder.Services.AddSingleton<IValidateOptions<ObservabilityOptions>, ObservabilityOptionsValidator>();
    builder.Services.AddOptions<ObservabilityOptions>()
        .Bind(builder.Configuration.GetSection(ObservabilityOptions.SectionName))
        .ValidateOnStart();
    var otlpEndpoint = builder.Configuration.GetSection(ObservabilityOptions.SectionName).Get<ObservabilityOptions>()?.OtlpEndpoint;
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(r => r.AddService("ClientManager.Api"))
        .WithMetrics(m =>
        {
            m.AddAspNetCoreInstrumentation();
            m.AddMeter(ClientManagerMetrics.MeterName);
            m.AddMeter(StorageMetrics.MeterName);
            m.AddView(instrument =>
            {
                if (instrument.Unit != "ms" || !instrument.Name.EndsWith(".duration", StringComparison.Ordinal))
                {
                    return null;
                }

                var meterName = instrument.Meter.Name;
                if (meterName != ClientManagerMetrics.MeterName && meterName != StorageMetrics.MeterName)
                {
                    return null;
                }

                return new ExplicitBucketHistogramConfiguration
                {
                    Boundaries = DurationHistogramBuckets.Milliseconds,
                };
            });
            m.AddPrometheusExporter();
        })
        .WithTracing(t => { t.AddAspNetCoreInstrumentation(); t.AddHttpClientInstrumentation(); t.AddSource(ClientManagerMetrics.ActivitySourceName); t.AddSource(StorageMetrics.ActivitySourceName); if (Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out var ep)) t.AddOtlpExporter(o => o.Endpoint = ep); });
    builder.Services.AddSwaggerGen(o => { o.SwaggerDoc("v2", new OpenApiInfo { Title = "ClientManager API", Version = "v2" }); o.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml")); o.DocumentFilter<TagDescriptionsDocumentFilter>(); });
    var app = builder.Build();
    app.UseMiddleware<RequestTrackingMiddleware>();
    app.UseMiddleware<ErrorHandlingMiddleware>();
    app.UseSwagger();
    app.UseSwaggerUI(o => { o.SwaggerEndpoint("/swagger/v2/swagger.json", "ClientManager API v2"); o.RoutePrefix = "docs"; });
    app.UseHttpsRedirection();
    app.UseAuthorization();
    app.MapControllers();
    app.MapPrometheusScrapingEndpoint("/prometheus/otel");
    app.Run();
}
catch (Exception ex) { logger.Error(ex, "Stopped program because of exception"); throw; }
finally { LogManager.Shutdown(); }
