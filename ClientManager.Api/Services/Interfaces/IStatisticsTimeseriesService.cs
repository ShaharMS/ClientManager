using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Chart-ready statistics timeseries search for dashboard and monitor charts.
/// </summary>
/// <remarks>
/// Implementations split closed snapshot reads from live counter overlay; see
/// <see cref="Storage.StatisticsTimeseriesService"/> for the caching model.
/// </remarks>
public interface IStatisticsTimeseriesService
{
    /// <summary>
    /// Returns bucketed granted/denied/active series for the requested targets, clients, and time range.
    /// </summary>
    /// <param name="request">Search category, optional target/client filters, UTC range, and display bucket count.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<TimeseriesSearchResponse> SearchAsync(
        TimeseriesSearchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Estimates service granted requests per minute over the last five minutes using snapshot tiers
    /// plus live counter overlay (same model as dashboard charts).
    /// </summary>
    Task<double> ComputeServiceRequestsPerMinuteAsync(CancellationToken cancellationToken = default);
}
