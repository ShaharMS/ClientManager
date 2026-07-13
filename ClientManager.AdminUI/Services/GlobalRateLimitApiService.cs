using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;

namespace ClientManager.AdminUI.Services;

/// <summary>
/// API client for global per-service rate limits. Each limit's <see cref="GlobalRateLimit.Id"/> is the service ID.
/// </summary>
public class GlobalRateLimitApiService(IHttpClientFactory httpClientFactory)
    : GenericApiService<GlobalRateLimit>(httpClientFactory, "api/v1/global-rate-limits")
{
    private (List<GlobalRateLimit> Data, DateTime At)? _cached;

    /// <summary>
    /// Returns all global service rate limits, cached for 30 seconds.
    /// </summary>
    public async Task<List<GlobalRateLimit>> GetAllServiceLimitsAsync()
    {
        if (_cached is { } hit && DateTime.UtcNow - hit.At < TimeSpan.FromSeconds(30))
        {
            return hit.Data;
        }

        var result = await SearchAsync(new DocumentQuery { Take = 100 });
        var data = result.Items.ToList();
        _cached = (data, DateTime.UtcNow);
        return data;
    }

    /// <summary>
    /// Returns the global rate limit for a service, if one exists.
    /// </summary>
    public async Task<GlobalRateLimit?> GetByServiceIdAsync(string serviceId)
    {
        var limits = await GetAllServiceLimitsAsync();
        return limits.FirstOrDefault(l => string.Equals(l.Id, serviceId, StringComparison.Ordinal));
    }

    public new async Task CreateAsync(GlobalRateLimit limit)
    {
        await base.CreateAsync(limit);
        _cached = null;
    }

    public new async Task UpdateAsync(string id, GlobalRateLimit limit)
    {
        await base.UpdateAsync(id, limit);
        _cached = null;
    }

    public new async Task DeleteAsync(string id)
    {
        await base.DeleteAsync(id);
        _cached = null;
    }
}
