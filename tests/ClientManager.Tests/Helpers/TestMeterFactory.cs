using System.Diagnostics.Metrics;
using Microsoft.Extensions.Diagnostics.Metrics;

namespace ClientManager.Tests.Helpers;

/// <summary>
/// Minimal <see cref="IMeterFactory"/> for unit tests.
/// </summary>
public sealed class TestMeterFactory : IMeterFactory
{
    public Meter Create(MeterOptions options) => new(options.Name);

    public void Dispose()
    {
    }
}
