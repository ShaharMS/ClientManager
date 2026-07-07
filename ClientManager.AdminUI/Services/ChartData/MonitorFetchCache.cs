using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.AdminUI.Services.ChartData;

internal sealed record MonitorFetchCache(
    string CacheKey,
    List<TargetClientUsageBreakdownResponse> Breakdowns,
    List<HistoricalUsageResponse> AllHistories,
    Dictionary<string, GlobalRateLimit> RateLimitLookup,
    List<Service> VisibleServices,
    bool IsAllServices,
    DateTime From,
    DateTime Now,
    TimeSpan RangeDuration,
    string Granularity,
    Dictionary<string, Dictionary<string, ClientHistoricalUsageResponse>> ClientHistoriesByService,
    Dictionary<string, HistoricalUsageResponse?> ServiceHistories);
