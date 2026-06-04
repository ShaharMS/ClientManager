using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Search;

namespace ClientManager.AdminUI.Services;

public class GlobalRateLimitApiService(IHttpClientFactory httpClientFactory)
    : GenericApiService<GlobalRateLimit>(httpClientFactory, "api/v1/global-rate-limits")
{
    private readonly Dictionary<TargetType, (List<GlobalRateLimit> Data, DateTime At)> _cachedByTarget = [];

    public async Task<List<GlobalRateLimit>> GetByTargetTypeAsync(TargetType targetType)
    {
        if (_cachedByTarget.TryGetValue(targetType, out var cached) && DateTime.UtcNow - cached.At < TimeSpan.FromSeconds(30))
        {
            return cached.Data;
        }

        var query = new DocumentQuery { Take = 100 }
            .Where(nameof(GlobalRateLimit.TargetType), FilterOperator.Equals, targetType.ToString());
        var result = await SearchAsync(query);
        var data = result.Items.ToList();
        _cachedByTarget[targetType] = (data, DateTime.UtcNow);
        return data;
    }

    public new async Task CreateAsync(GlobalRateLimit limit)
    {
        await base.CreateAsync(limit);
        _cachedByTarget.Clear();
    }

    public new async Task UpdateAsync(string id, GlobalRateLimit limit)
    {
        await base.UpdateAsync(id, limit);
        _cachedByTarget.Clear();
    }

    public new async Task DeleteAsync(string id)
    {
        await base.DeleteAsync(id);
        _cachedByTarget.Clear();
    }
}
