using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Chart-ready statistics timeseries search.
/// </summary>
public interface IStatisticsTimeseriesService
{
    Task<TimeseriesSearchResponse> SearchAsync(
        TimeseriesSearchRequest request,
        CancellationToken cancellationToken = default);
}
