using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Provides read-only dashboard statistics: overview and timeseries search.
/// </summary>
public interface IStatisticsService
{
    Task<SystemOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken = default);

    Task<TimeseriesSearchResponse> SearchTimeseriesAsync(
        TimeseriesSearchRequest request,
        CancellationToken cancellationToken = default);
}
