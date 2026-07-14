using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ClientManager.Api.Services.Storage.Instrumentation;

/// <summary>
/// OpenTelemetry instruments for the in-process storage domain.
/// Activity spans are emitted from <c>"ClientManager.Storage"</c>. Hot-path
/// histograms record access checks, rate-limit strategy work, and document-store
/// operations in milliseconds.
/// </summary>
public class StorageMetrics
{
    public static readonly string MeterName = "ClientManager.Storage";
    public static readonly string ActivitySourceName = "ClientManager.Storage";

    private readonly Meter _meter;

    public ActivitySource ActivitySource { get; }

    public Counter<long> RateLimitAllowed { get; }
    public Counter<long> RateLimitDenied { get; }
    public Counter<long> GlobalRateLimitHits { get; }
    public Counter<long> AccessDenied { get; }
    public Counter<long> CatalogCacheLookups { get; }
    public Histogram<double> CatalogCacheDuration { get; }
    public Histogram<double> AccessCheckDuration { get; }
    public Histogram<double> RateLimitStrategyDuration { get; }
    public Histogram<double> DocumentStoreOperationDuration { get; }

    public StorageMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);
        ActivitySource = new(ActivitySourceName);

        RateLimitAllowed = CreateCounter("clientmanager.ratelimit.allowed", "Rate limit checks that passed.");
        RateLimitDenied = CreateCounter("clientmanager.ratelimit.denied", "Rate limit checks that were denied.");
        GlobalRateLimitHits = CreateCounter("clientmanager.ratelimit.global_hits", "Global rate limit denials.");
        AccessDenied = CreateCounter("clientmanager.access.denied", "Access checks that were denied.");
        CatalogCacheLookups = CreateCounter("clientmanager.storage.catalog_cache", "Catalog read-cache lookups.");
        CatalogCacheDuration = CreateHistogram("clientmanager.storage.catalog_cache.duration", "ms", "Catalog read-cache lookup duration in milliseconds.");
        AccessCheckDuration = CreateHistogram("clientmanager.storage.access.duration", "ms", "Storage access-check duration in milliseconds.");
        RateLimitStrategyDuration = CreateHistogram("clientmanager.storage.ratelimit.strategy.duration", "ms", "Rate-limit strategy evaluation duration in milliseconds.");
        DocumentStoreOperationDuration = CreateHistogram("clientmanager.storage.document_store.duration", "ms", "Document-store operation duration in milliseconds.");
    }

    private Counter<long> CreateCounter(string name, string description) =>
        _meter.CreateCounter<long>(name, description: description);

    private Histogram<double> CreateHistogram(string name, string unit, string description) =>
        _meter.CreateHistogram<double>(name, unit: unit, description: description);
}
