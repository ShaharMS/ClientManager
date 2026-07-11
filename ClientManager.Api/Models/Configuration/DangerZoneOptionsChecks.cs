using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace ClientManager.Api.Models.Configuration;

/// <summary>ponytail: assert-only guard for DangerZone binding and defaults; run via dotnet run -- --danger-zone-check.</summary>
internal static class DangerZoneOptionsChecks
{
    internal static int Run()
    {
        var productionConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var productionOptions = new DangerZoneOptions();
        DangerZoneOptionsPostConfigure.ApplyDefaults(productionOptions, isDevelopment: false);

        if (productionOptions.IsStartupSeedingEnabled)
        {
            return 1;
        }

        if (productionOptions.IsSeedExportEnabled)
        {
            return 2;
        }

        if (productionOptions.IsSeedImportEnabled)
        {
            return 3;
        }

        if (!productionOptions.IsUsagePruningEnabled)
        {
            return 4;
        }

        var developmentOptions = new DangerZoneOptions();
        DangerZoneOptionsPostConfigure.ApplyDefaults(developmentOptions, isDevelopment: true);

        if (!developmentOptions.IsStartupSeedingEnabled ||
            !developmentOptions.IsSeedExportEnabled ||
            !developmentOptions.IsSeedImportEnabled)
        {
            return 5;
        }

        var explicitProduction = new DangerZoneOptions
        {
            EnableSeedExport = true,
            EnableSeedImport = false
        };
        DangerZoneOptionsPostConfigure.ApplyDefaults(explicitProduction, isDevelopment: false);

        if (!explicitProduction.IsSeedExportEnabled || explicitProduction.IsSeedImportEnabled)
        {
            return 6;
        }

        var invalidTtl = new DangerZoneOptions
        {
            StorageReadCache = new ClientManager.Shared.Configuration.Storage.StorageReadCacheOptions
            {
                CatalogTtl = TimeSpan.Zero
            }
        };
        var validator = new DangerZoneOptionsValidator();
        if (validator.Validate(null, invalidTtl) == ValidateOptionsResult.Success)
        {
            return 7;
        }

        var boundConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{DangerZoneOptions.SectionName}:{DangerZoneOptions.StorageReadCacheSubsection}:CatalogTtl"] = "00:02:00",
                [$"{DangerZoneOptions.SectionName}:EnableSeedImport"] = "true"
            })
            .Build();

        var bound = boundConfig.GetSection(DangerZoneOptions.SectionName).Get<DangerZoneOptions>();
        if (bound is null ||
            bound.StorageReadCache.CatalogTtl != TimeSpan.FromMinutes(2) ||
            bound.EnableSeedImport != true)
        {
            return 8;
        }

        return 0;
    }
}
