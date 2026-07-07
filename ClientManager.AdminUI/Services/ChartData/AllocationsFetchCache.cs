using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.AdminUI.Services.ChartData;

internal sealed record AllocationsFetchCache(
    string CacheKey,
    List<ResourcePoolStatisticsResponse> Pools,
    List<TargetClientUsageBreakdownResponse> Breakdowns,
    List<TargetClientUsageBreakdownResponse> RecentBreakdowns,
    List<TargetClientUsageBreakdownResponse> SummaryRecentBreakdowns,
    Dictionary<string, GlobalRateLimit> RateLimitLookup,
    List<ResourcePoolStatisticsResponse> VisiblePools,
    bool IsAllPools,
    List<HistoricalUsageResponse> AllHistories,
    Dictionary<string, Dictionary<string, ClientHistoricalUsageResponse>> ClientHistoriesByPool,
    Dictionary<string, HistoricalUsageResponse?> PoolHistories,
    DateTime From,
    DateTime Now,
    string Granularity,
    bool IsAccessMetric);
