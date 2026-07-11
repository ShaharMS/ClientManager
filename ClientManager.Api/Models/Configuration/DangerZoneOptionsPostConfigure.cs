using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ClientManager.Api.Models.Configuration;

/// <summary>
/// Applies environment-specific defaults to unset <see cref="DangerZoneOptions"/> gates.
/// </summary>
public sealed class DangerZoneOptionsPostConfigure(IHostEnvironment environment) : IConfigureOptions<DangerZoneOptions>
{
    /// <inheritdoc />
    public void Configure(DangerZoneOptions options)
    {
        ApplyDefaults(options, environment);
    }

    /// <summary>
    /// Resolves nullable gates using environment-specific defaults.
    /// </summary>
    internal static void ApplyDefaults(DangerZoneOptions options, IHostEnvironment environment)
    {
        ApplyDefaults(options, environment.IsDevelopment());
    }

    /// <summary>
    /// Resolves nullable gates for the given development flag.
    /// </summary>
    internal static void ApplyDefaults(DangerZoneOptions options, bool isDevelopment)
    {
        options.EnableStartupSeeding ??= isDevelopment;
        options.EnableSeedExport ??= isDevelopment;
        options.EnableSeedImport ??= isDevelopment;
        options.EnableUsagePruning ??= true;
    }
}
