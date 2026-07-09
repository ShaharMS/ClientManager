using System.Diagnostics;
using ClientManager.AdminUI.Utils;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.AdminUI.Services.ChartData;

public static class MonitorCapCalculator
{
    public static int GetEffectiveClientServiceCap(
        string clientId,
        string serviceId,
        IReadOnlyList<ClientConfiguration> allClients,
        IReadOnlyDictionary<string, GlobalRateLimit> globalLimitsByService,
        TimeSpan comparisonWindow)
    {
        var client = allClients.FirstOrDefault(c => c.Id == clientId);
        if (client is null)
        {
            return GetScaledGlobalServiceCap(serviceId, globalLimitsByService, comparisonWindow);
        }

        var serviceSettings = client.Services.GetValueOrDefault(serviceId);
        var caps = new List<int>();

        if (serviceSettings?.RateLimit is not null)
        {
            caps.Add(RateLimitCapScaler.ScaleRateLimitCap(
                serviceSettings.RateLimit.MaxRequests,
                serviceSettings.RateLimit.Window,
                comparisonWindow,
                serviceSettings.RateLimit.Strategy));
        }

        if (client.GlobalRateLimit is not null)
        {
            caps.Add(RateLimitCapScaler.ScaleRateLimitCap(
                client.GlobalRateLimit.MaxRequests,
                client.GlobalRateLimit.Window,
                comparisonWindow,
                client.GlobalRateLimit.Strategy));
        }

        var exemptFromServiceGlobal = serviceSettings?.ExemptFromGlobalLimit ?? client.ExemptFromGlobalLimits;
        if (!exemptFromServiceGlobal)
        {
            var globalCap = GetScaledGlobalServiceCap(serviceId, globalLimitsByService, comparisonWindow);
            if (globalCap > 0)
            {
                caps.Add(globalCap);
            }
        }

        return caps.Count > 0 ? caps.Min() : 0;
    }

    public static int GetScaledGlobalServiceCap(
        string serviceId,
        IReadOnlyDictionary<string, GlobalRateLimit> globalLimitsByService,
        TimeSpan comparisonWindow)
    {
        return globalLimitsByService.TryGetValue(serviceId, out var globalLimit)
            ? RateLimitCapScaler.ScaleRateLimitCap(
                globalLimit.MaxRequests,
                globalLimit.Window,
                comparisonWindow,
                globalLimit.Strategy)
            : 0;
    }

    public static bool ContributesToGlobalPoolLimit(
        string clientId,
        IReadOnlyList<ClientConfiguration> allClients)
    {
        var client = allClients.FirstOrDefault(c => c.Id == clientId);
        return client?.ContributesToGlobalLimits ?? true;
    }

    public static bool ContributesToGlobalServiceLimit(
        string clientId,
        string serviceId,
        IReadOnlyList<ClientConfiguration> allClients)
    {
        var client = allClients.FirstOrDefault(c => c.Id == clientId);
        if (client is null)
        {
            return true;
        }

        var serviceSettings = client.Services.GetValueOrDefault(serviceId);
        return serviceSettings?.ContributesToGlobalLimit ?? client.ContributesToGlobalLimits;
    }

    public static (long ContributingUsage, long OffBudgetUsage) PartitionServiceUsage(
        IReadOnlyList<ClientUsageEntry> entries,
        string serviceId,
        IReadOnlyList<ClientConfiguration> allClients)
    {
        long contributing = 0;
        long offBudget = 0;

        foreach (var entry in entries)
        {
            if (ContributesToGlobalServiceLimit(entry.ClientId, serviceId, allClients))
            {
                contributing += entry.GrantedCount;
            }
            else
            {
                offBudget += entry.GrantedCount;
            }
        }

        return (contributing, offBudget);
    }

    public static (int Cap, bool UsesGlobalCap) GetServiceSummaryCap(
        string serviceId,
        IReadOnlyList<ClientUsageEntry> entries,
        IReadOnlyList<ClientConfiguration> allClients,
        IReadOnlyDictionary<string, GlobalRateLimit> globalLimitsByService,
        TimeSpan comparisonWindow)
    {
        var globalCap = GetScaledGlobalServiceCap(serviceId, globalLimitsByService, comparisonWindow);
        if (globalCap > 0)
        {
            return (globalCap, true);
        }

        if (entries.Count == 0 || !entries.All(e => ClientHasServiceRateLimit(e.ClientId, serviceId, allClients)))
        {
            return (0, false);
        }

        var aggregateCap = entries.Sum(e =>
            GetEffectiveClientServiceCap(e.ClientId, serviceId, allClients, globalLimitsByService, comparisonWindow));

        return aggregateCap > 0 ? (aggregateCap, false) : (0, false);
    }

    public static long GetUtilizationUsage(long contributingUsage, long offBudgetUsage, bool usesGlobalCap) =>
        usesGlobalCap ? contributingUsage : contributingUsage + offBudgetUsage;

    internal static bool ClientHasServiceRateLimit(
        string clientId,
        string serviceId,
        IReadOnlyList<ClientConfiguration> allClients)
    {
        var client = allClients.FirstOrDefault(c => c.Id == clientId);
        if (client is null)
        {
            return false;
        }

        var serviceSettings = client.Services.GetValueOrDefault(serviceId);
        return serviceSettings?.RateLimit is not null || client.GlobalRateLimit is not null;
    }

    // ponytail: self-check only; upgrade path is a proper xUnit project when one exists
    public static void SelfCheck()
    {
        var window = TimeSpan.FromMinutes(5);
        var globalLimits = new Dictionary<string, GlobalRateLimit>
        {
            ["svc"] = new()
            {
                Id = "g1",
                TargetId = "svc",
                MaxRequests = 10,
                Window = TimeSpan.FromMinutes(1)
            }
        };

        var clients = new List<ClientConfiguration>
        {
            new()
            {
                Id = "c1",
                Name = "Contributor",
                ContributesToGlobalLimits = true,
                Services = new Dictionary<string, ServiceAccessSettings>
                {
                    ["svc"] = new() { IsAllowed = true }
                }
            },
            new()
            {
                Id = "c2",
                Name = "OffBudget",
                ContributesToGlobalLimits = false,
                Services = new Dictionary<string, ServiceAccessSettings>
                {
                    ["svc"] = new() { IsAllowed = true, ContributesToGlobalLimit = false }
                }
            }
        };

        var entries = new List<ClientUsageEntry>
        {
            new("c1", "Contributor", 40, 0, 0, 0, 0, 0, 0),
            new("c2", "OffBudget", 1238, 0, 0, 0, 0, 0, 0)
        };

        var (contributing, offBudget) = PartitionServiceUsage(entries, "svc", clients);
        Debug.Assert(contributing == 40 && offBudget == 1238);

        var (globalCap, usesGlobal) = GetServiceSummaryCap("svc", entries, clients, globalLimits, window);
        Debug.Assert(usesGlobal && globalCap == 50);

        var noGlobalClients = new List<ClientConfiguration>
        {
            new()
            {
                Id = "a",
                Name = "A",
                Services = new Dictionary<string, ServiceAccessSettings>
                {
                    ["svc2"] = new()
                    {
                        IsAllowed = true,
                        RateLimit = new ClientRateLimit { MaxRequests = 100, Window = TimeSpan.FromMinutes(1) }
                    }
                }
            },
            new()
            {
                Id = "b",
                Name = "B",
                Services = new Dictionary<string, ServiceAccessSettings>
                {
                    ["svc2"] = new() { IsAllowed = true }
                }
            }
        };

        var mixedEntries = new List<ClientUsageEntry>
        {
            new("a", "A", 10, 0, 0, 0, 0, 0, 0),
            new("b", "B", 5, 0, 0, 0, 0, 0, 0)
        };

        var (noCap, _) = GetServiceSummaryCap("svc2", mixedEntries, noGlobalClients, globalLimits, window);
        Debug.Assert(noCap == 0);

        var cappedClients = new List<ClientConfiguration>
        {
            new()
            {
                Id = "a",
                Name = "A",
                Services = new Dictionary<string, ServiceAccessSettings>
                {
                    ["svc2"] = new()
                    {
                        IsAllowed = true,
                        RateLimit = new ClientRateLimit { MaxRequests = 100, Window = TimeSpan.FromMinutes(1) }
                    }
                }
            },
            new()
            {
                Id = "b",
                Name = "B",
                GlobalRateLimit = new ClientRateLimit { MaxRequests = 50, Window = TimeSpan.FromMinutes(1) },
                Services = new Dictionary<string, ServiceAccessSettings>
                {
                    ["svc2"] = new() { IsAllowed = true }
                }
            }
        };

        var (aggCap, aggGlobal) = GetServiceSummaryCap("svc2", mixedEntries, cappedClients, globalLimits, window);
        Debug.Assert(!aggGlobal && aggCap == 750);

        var tokenBucketLimits = new Dictionary<string, GlobalRateLimit>
        {
            ["tb"] = new()
            {
                Id = "tb1",
                TargetId = "tb",
                MaxRequests = 100,
                Window = TimeSpan.FromMinutes(1),
                Strategy = RateLimitStrategy.TokenBucket
            }
        };

        var tokenBucketCap = GetScaledGlobalServiceCap("tb", tokenBucketLimits, TimeSpan.FromMinutes(5));
        Debug.Assert(tokenBucketCap == 100);

        var partialServices = new List<Service>
        {
            new() { Id = "limited", Name = "Limited" },
            new() { Id = "open", Name = "Open" }
        };
        var partialLookup = new Dictionary<string, GlobalRateLimit>
        {
            ["limited"] = globalLimits["svc"]
        };
        Debug.Assert(ChartCapResolver.ResolveAllServicesChartCap(
            partialServices, partialLookup, window) == 0);
    }
}
