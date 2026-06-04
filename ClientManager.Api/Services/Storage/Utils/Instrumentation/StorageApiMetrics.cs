using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ClientManager.Api.Services.Storage.Utils.Instrumentation;

/// <summary>
/// Holds request-level OpenTelemetry instruments for the internal storage API.
/// Activity spans are emitted from <c>"ClientManager.StorageApi"</c>. Hot-path
/// operation histograms record access checks, resource acquire/release calls,
/// rate-limit strategy work, and document-store operations in milliseconds.
/// </summary>
public class StorageApiMetrics
{
    public static readonly string MeterName = "ClientManager.StorageApi";
    public static readonly string ActivitySourceName = "ClientManager.StorageApi";

    private readonly Meter _meter;

    public ActivitySource ActivitySource { get; }

    public Counter<long> RequestsTotal { get; }
    public Counter<long> RequestErrors { get; }
    public Histogram<double> RequestDuration { get; }
    public Counter<long> RateLimitAllowed { get; }
    public Counter<long> RateLimitDenied { get; }
    public Counter<long> GlobalRateLimitHits { get; }
    public Counter<long> ResourceAcquired { get; }
    public Counter<long> ResourceReleased { get; }
    public Counter<long> ResourceDenied { get; }
    public Counter<long> ResourceExpired { get; }
    public Counter<long> AccessGranted { get; }
    public Counter<long> AccessDenied { get; }
    public Histogram<double> AccessCheckDuration { get; }
    public Histogram<double> ResourceAcquireDuration { get; }
    public Histogram<double> ResourceReleaseDuration { get; }
    public Histogram<double> RateLimitStrategyDuration { get; }
    public Histogram<double> DocumentStoreOperationDuration { get; }

    public StorageApiMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);
        ActivitySource = new(ActivitySourceName);

        RequestsTotal = CreateCounter("clientmanager.storageapi.requests.total", "Total HTTP requests received by the storage API.");
        RequestErrors = CreateCounter("clientmanager.storageapi.requests.errors", "Total storage API HTTP request errors.");
        RequestDuration = CreateHistogram("clientmanager.storageapi.requests.duration", "ms", "Storage API HTTP request duration in milliseconds.");
        RateLimitAllowed = CreateCounter("clientmanager.ratelimit.allowed", "Rate limit checks that passed.");
        RateLimitDenied = CreateCounter("clientmanager.ratelimit.denied", "Rate limit checks that were denied.");
        GlobalRateLimitHits = CreateCounter("clientmanager.ratelimit.global_hits", "Global rate limit denials.");
        ResourceAcquired = CreateCounter("clientmanager.resources.acquired", "Resource slots successfully acquired.");
        ResourceReleased = CreateCounter("clientmanager.resources.released", "Resource slots released.");
        ResourceDenied = CreateCounter("clientmanager.resources.denied", "Resource acquisition attempts denied.");
        ResourceExpired = CreateCounter("clientmanager.resources.expired", "Resource allocations expired by cleanup.");
        AccessGranted = CreateCounter("clientmanager.access.granted", "Access checks that passed.");
        AccessDenied = CreateCounter("clientmanager.access.denied", "Access checks that were denied.");
        AccessCheckDuration = CreateHistogram("clientmanager.storageapi.access.duration", "ms", "Storage API access-check duration in milliseconds.");
        ResourceAcquireDuration = CreateHistogram("clientmanager.storageapi.resources.acquire.duration", "ms", "Storage API resource-acquire duration in milliseconds.");
        ResourceReleaseDuration = CreateHistogram("clientmanager.storageapi.resources.release.duration", "ms", "Storage API resource-release duration in milliseconds.");
        RateLimitStrategyDuration = CreateHistogram("clientmanager.storageapi.ratelimit.strategy.duration", "ms", "Rate-limit strategy evaluation duration in milliseconds.");
        DocumentStoreOperationDuration = CreateHistogram("clientmanager.storageapi.document_store.duration", "ms", "Document-store operation duration in milliseconds.");
    }

    private Counter<long> CreateCounter(string name, string description) =>
        _meter.CreateCounter<long>(name, description: description);

    private Histogram<double> CreateHistogram(string name, string unit, string description) =>
        _meter.CreateHistogram<double>(name, unit: unit, description: description);
}
