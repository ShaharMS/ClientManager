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
/// histograms record HTTP request timing.
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

    public ClientManagerMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);
        ActivitySource = new(ActivitySourceName);

        RequestsTotal = CreateCounter("clientmanager.requests.total", "Total HTTP requests received");
        RequestErrors = CreateCounter("clientmanager.requests.errors", "Total HTTP request errors");
        RequestDuration = CreateHistogram("clientmanager.requests.duration", "ms", "HTTP request duration in milliseconds");
    }

    private Counter<long> CreateCounter(string name, string description) =>
        _meter.CreateCounter<long>(name, description: description);

    private Histogram<double> CreateHistogram(string name, string unit, string description) =>
        _meter.CreateHistogram<double>(name, unit: unit, description: description);
}
