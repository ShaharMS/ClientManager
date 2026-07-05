namespace ClientManager.Api.Utils;

/// <summary>
/// Catalog collections supported by the seed export/import API.
/// </summary>
[Flags]
public enum SeedCollections
{
    None = 0,
    Services = 1,
    ResourcePools = 2,
    GlobalRateLimits = 4,
    ClientConfigurations = 8,
    All = Services | ResourcePools | GlobalRateLimits | ClientConfigurations
}

/// <summary>
/// Parses <c>?include=</c> query values for seed endpoints.
/// </summary>
public static class SeedCollectionParser
{
    private static readonly Dictionary<string, SeedCollections> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["services"] = SeedCollections.Services,
        ["resourcePools"] = SeedCollections.ResourcePools,
        ["resource-pools"] = SeedCollections.ResourcePools,
        ["globalRateLimits"] = SeedCollections.GlobalRateLimits,
        ["global-rate-limits"] = SeedCollections.GlobalRateLimits,
        ["clientConfigurations"] = SeedCollections.ClientConfigurations,
        ["client-configurations"] = SeedCollections.ClientConfigurations,
        ["clients"] = SeedCollections.ClientConfigurations
    };

    /// <summary>
    /// Parses a comma-separated include list. Returns <see cref="SeedCollections.All"/> when empty.
    /// </summary>
    public static SeedCollections Parse(string? include)
    {
        if (string.IsNullOrWhiteSpace(include))
        {
            return SeedCollections.All;
        }

        var selected = SeedCollections.None;
        foreach (var token in include.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Aliases.TryGetValue(token, out var collection))
            {
                throw new ArgumentException($"Unknown seed collection '{token}'.");
            }

            selected |= collection;
        }

        return selected == SeedCollections.None ? SeedCollections.All : selected;
    }
}
