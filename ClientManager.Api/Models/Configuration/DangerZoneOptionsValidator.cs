using Microsoft.Extensions.Options;

namespace ClientManager.Api.Models.Configuration;

/// <summary>
/// Validates <see cref="DangerZoneOptions"/> at startup, including nested cache TTL invariants.
/// </summary>
public sealed class DangerZoneOptionsValidator : IValidateOptions<DangerZoneOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, DangerZoneOptions options)
    {
        var cache = options.StorageReadCache;
        var prefix = $"{DangerZoneOptions.SectionName}:{DangerZoneOptions.StorageReadCacheSubsection}";

        if (cache.CatalogTtl <= TimeSpan.Zero)
        {
            return ValidateOptionsResult.Fail($"{prefix}:CatalogTtl must be positive.");
        }

        if (cache.HotPathCatalogTtl <= TimeSpan.Zero)
        {
            return ValidateOptionsResult.Fail($"{prefix}:HotPathCatalogTtl must be positive.");
        }

        if (cache.StatisticsTtl <= TimeSpan.Zero)
        {
            return ValidateOptionsResult.Fail($"{prefix}:StatisticsTtl must be positive.");
        }

        return ValidateOptionsResult.Success;
    }
}
