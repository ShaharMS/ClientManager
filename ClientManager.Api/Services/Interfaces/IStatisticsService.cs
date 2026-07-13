using ClientManager.Shared.Models.Responses;

namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Provides read-only dashboard statistics.
/// </summary>
public interface IStatisticsService
{
    Task<SystemOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken = default);
}
