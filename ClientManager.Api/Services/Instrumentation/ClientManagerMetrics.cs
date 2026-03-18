using System.Diagnostics.Metrics;

namespace ClientManager.Api.Services.Instrumentation;

/// <summary>
/// Holds all OpenTelemetry meter and instrument instances for ClientManager.
/// </summary>
public class ClientManagerMetrics
{
    public static readonly string MeterName = "ClientManager";

    private readonly Meter _meter;

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

    public ClientManagerMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);

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
    }
}
