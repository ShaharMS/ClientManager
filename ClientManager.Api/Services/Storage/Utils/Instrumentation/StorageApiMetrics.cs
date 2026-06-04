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
        ActivitySource = new ActivitySource(ActivitySourceName);

        RequestsTotal = _meter.CreateCounter<long>(
            "clientmanager.storageapi.requests.total",
            description: "Total HTTP requests received by the storage API.");

        RequestErrors = _meter.CreateCounter<long>(
            "clientmanager.storageapi.requests.errors",
            description: "Total storage API HTTP request errors.");

        RequestDuration = _meter.CreateHistogram<double>(
            "clientmanager.storageapi.requests.duration",
            unit: "ms",
            description: "Storage API HTTP request duration in milliseconds.");

        RateLimitAllowed = _meter.CreateCounter<long>(
            "clientmanager.ratelimit.allowed",
            description: "Rate limit checks that passed.");

        RateLimitDenied = _meter.CreateCounter<long>(
            "clientmanager.ratelimit.denied",
            description: "Rate limit checks that were denied.");

        GlobalRateLimitHits = _meter.CreateCounter<long>(
            "clientmanager.ratelimit.global_hits",
            description: "Global rate limit denials.");

        ResourceAcquired = _meter.CreateCounter<long>(
            "clientmanager.resources.acquired",
            description: "Resource slots successfully acquired.");

        ResourceReleased = _meter.CreateCounter<long>(
            "clientmanager.resources.released",
            description: "Resource slots released.");

        ResourceDenied = _meter.CreateCounter<long>(
            "clientmanager.resources.denied",
            description: "Resource acquisition attempts denied.");

        ResourceExpired = _meter.CreateCounter<long>(
            "clientmanager.resources.expired",
            description: "Resource allocations expired by cleanup.");

        AccessGranted = _meter.CreateCounter<long>(
            "clientmanager.access.granted",
            description: "Access checks that passed.");

        AccessDenied = _meter.CreateCounter<long>(
            "clientmanager.access.denied",
            description: "Access checks that were denied.");

        AccessCheckDuration = _meter.CreateHistogram<double>(
            "clientmanager.storageapi.access.duration",
            unit: "ms",
            description: "Storage API access-check duration in milliseconds.");

        ResourceAcquireDuration = _meter.CreateHistogram<double>(
            "clientmanager.storageapi.resources.acquire.duration",
            unit: "ms",
            description: "Storage API resource-acquire duration in milliseconds.");

        ResourceReleaseDuration = _meter.CreateHistogram<double>(
            "clientmanager.storageapi.resources.release.duration",
            unit: "ms",
            description: "Storage API resource-release duration in milliseconds.");

        RateLimitStrategyDuration = _meter.CreateHistogram<double>(
            "clientmanager.storageapi.ratelimit.strategy.duration",
            unit: "ms",
            description: "Rate-limit strategy evaluation duration in milliseconds.");

        DocumentStoreOperationDuration = _meter.CreateHistogram<double>(
            "clientmanager.storageapi.document_store.duration",
            unit: "ms",
            description: "Document-store operation duration in milliseconds.");
    }
}