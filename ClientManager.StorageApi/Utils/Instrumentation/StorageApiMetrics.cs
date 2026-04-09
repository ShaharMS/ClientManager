using System.Diagnostics.Metrics;

namespace ClientManager.StorageApi.Utils.Instrumentation;

/// <summary>
/// Holds request-level OpenTelemetry instruments for the internal storage API.
/// </summary>
public class StorageApiMetrics
{
    public static readonly string MeterName = "ClientManager.StorageApi";

    private readonly Meter _meter;

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

    public StorageApiMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);

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
    }
}