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

    // Request tracking
    public Counter<long> RequestsTotal { get; }
    public Counter<long> RequestErrors { get; }
    public Histogram<double> RequestDuration { get; }

    // Rate limiting
    public Counter<long> RateLimitAllowed { get; }
    public Counter<long> RateLimitDenied { get; }
    public Counter<long> GlobalRateLimitHits { get; }

    // Resource allocation
    public Counter<long> ResourceAcquired { get; }
    public Counter<long> ResourceReleased { get; }
    public Counter<long> ResourceDenied { get; }
    public Counter<long> ResourceExpired { get; }

    // Access control
    public Counter<long> AccessGranted { get; }
    public Counter<long> AccessDenied { get; }

    // Hot-path operation durations
    public Histogram<double> AccessCheckDuration { get; }
    public Histogram<double> ResourceAcquireDuration { get; }
    public Histogram<double> ResourceReleaseDuration { get; }
    public Histogram<double> StorageClientCallDuration { get; }

    public ClientManagerMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);
        ActivitySource = new ActivitySource(ActivitySourceName);

        RequestsTotal = _meter.CreateCounter<long>(
            "clientmanager.requests.total",
            description: "Total HTTP requests received");

        RequestErrors = _meter.CreateCounter<long>(
            "clientmanager.requests.errors",
            description: "Total HTTP request errors");

        RequestDuration = _meter.CreateHistogram<double>(
            "clientmanager.requests.duration",
            unit: "ms",
            description: "HTTP request duration in milliseconds");

        RateLimitAllowed = _meter.CreateCounter<long>(
            "clientmanager.ratelimit.allowed",
            description: "Rate limit checks that passed");

        RateLimitDenied = _meter.CreateCounter<long>(
            "clientmanager.ratelimit.denied",
            description: "Rate limit checks that were denied");

        GlobalRateLimitHits = _meter.CreateCounter<long>(
            "clientmanager.ratelimit.global_hits",
            description: "Global rate limit denials");

        ResourceAcquired = _meter.CreateCounter<long>(
            "clientmanager.resources.acquired",
            description: "Resource slots successfully acquired");

        ResourceReleased = _meter.CreateCounter<long>(
            "clientmanager.resources.released",
            description: "Resource slots released");

        ResourceDenied = _meter.CreateCounter<long>(
            "clientmanager.resources.denied",
            description: "Resource acquisition attempts denied");

        ResourceExpired = _meter.CreateCounter<long>(
            "clientmanager.resources.expired",
            description: "Resource allocations expired by cleanup");

        AccessGranted = _meter.CreateCounter<long>(
            "clientmanager.access.granted",
            description: "Access checks that passed");

        AccessDenied = _meter.CreateCounter<long>(
            "clientmanager.access.denied",
            description: "Access checks that were denied");

        AccessCheckDuration = _meter.CreateHistogram<double>(
            "clientmanager.access.duration",
            unit: "ms",
            description: "Public API access-check duration in milliseconds");

        ResourceAcquireDuration = _meter.CreateHistogram<double>(
            "clientmanager.resources.acquire.duration",
            unit: "ms",
            description: "Public API resource-acquire duration in milliseconds");

        ResourceReleaseDuration = _meter.CreateHistogram<double>(
            "clientmanager.resources.release.duration",
            unit: "ms",
            description: "Public API resource-release duration in milliseconds");

        StorageClientCallDuration = _meter.CreateHistogram<double>(
            "clientmanager.storage_client.duration",
            unit: "ms",
            description: "Public API outbound storage-client call duration in milliseconds");
    }
}
