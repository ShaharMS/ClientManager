using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ClientManager.Api.Utils.Instrumentation;

/// <summary>
/// Holds all OpenTelemetry meter and instrument instances for ClientManager.
/// <para>
/// Registered as a singleton and injected into services and middleware that emit metrics.
/// All instruments share a single <see cref="Meter"/> named
/// <c>"ClientManager"</c>, which external collectors (Prometheus, OTLP) can subscribe to.
/// Activity spans are emitted from <c>"ClientManager.Api"</c>. Hot-path operation
/// histograms record access checks, resource acquire/release calls, and storage-client
/// round trips in milliseconds.
/// </para>
/// </summary>
public class ClientManagerMetrics
{
    public static readonly string MeterName = "ClientManager";
    public static readonly string ActivitySourceName = "ClientManager.Api";

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
    public Histogram<double> StorageClientCallDuration { get; }

    public ClientManagerMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);
        ActivitySource = new(ActivitySourceName);

        RequestsTotal = CreateCounter("clientmanager.requests.total", "Total HTTP requests received");
        RequestErrors = CreateCounter("clientmanager.requests.errors", "Total HTTP request errors");
        RequestDuration = CreateHistogram("clientmanager.requests.duration", "ms", "HTTP request duration in milliseconds");
        RateLimitAllowed = CreateCounter("clientmanager.ratelimit.allowed", "Rate limit checks that passed");
        RateLimitDenied = CreateCounter("clientmanager.ratelimit.denied", "Rate limit checks that were denied");
        GlobalRateLimitHits = CreateCounter("clientmanager.ratelimit.global_hits", "Global rate limit denials");
        ResourceAcquired = CreateCounter("clientmanager.resources.acquired", "Resource slots successfully acquired");
        ResourceReleased = CreateCounter("clientmanager.resources.released", "Resource slots released");
        ResourceDenied = CreateCounter("clientmanager.resources.denied", "Resource acquisition attempts denied");
        ResourceExpired = CreateCounter("clientmanager.resources.expired", "Resource allocations expired by cleanup");
        AccessGranted = CreateCounter("clientmanager.access.granted", "Access checks that passed");
        AccessDenied = CreateCounter("clientmanager.access.denied", "Access checks that were denied");
        AccessCheckDuration = CreateHistogram("clientmanager.access.duration", "ms", "Public API access-check duration in milliseconds");
        ResourceAcquireDuration = CreateHistogram("clientmanager.resources.acquire.duration", "ms", "Public API resource-acquire duration in milliseconds");
        ResourceReleaseDuration = CreateHistogram("clientmanager.resources.release.duration", "ms", "Public API resource-release duration in milliseconds");
        StorageClientCallDuration = CreateHistogram("clientmanager.storage_client.duration", "ms", "Public API outbound storage-client call duration in milliseconds");
    }

    private Counter<long> CreateCounter(string name, string description) =>
        _meter.CreateCounter<long>(name, description: description);

    private Histogram<double> CreateHistogram(string name, string unit, string description) =>
        _meter.CreateHistogram<double>(name, unit: unit, description: description);
}
