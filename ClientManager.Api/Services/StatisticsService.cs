using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Services.Storage;
using ClientManager.Api.Storage.Databases.Interfaces;
using ClientManager.Api.Storage.Repositories.Interfaces;
using ClientManager.Shared.Models.Responses;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services;

/// <summary>
/// Composes dashboard statistics from catalog data and RPM accounting.
/// </summary>
/// <remarks>
/// <para>
/// Implements <see cref="IStatisticsService"/> with inexpensive count queries for clients and
/// services plus the shared RPM ring average. Keeps the dashboard responsive without reintroducing
/// historical timeseries storage.
/// </para>
/// </remarks>
public sealed class StatisticsService : IStatisticsService
{
    private readonly IClientConfigurationDatabase _clients;
    private readonly IEntityRepository<ClientManager.Shared.Models.Entities.Service> _services;
    private readonly RpmAccountingService _rpm;

    public StatisticsService(
        IClientConfigurationDatabase clients,
        IEntityRepository<ClientManager.Shared.Models.Entities.Service> services,
        RpmAccountingService rpm)
    {
        _clients = clients;
        _services = services;
        _rpm = rpm;
    }

    public async Task<SystemOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var totalClients = await _clients.CountAsync(DocumentQuery.All, cancellationToken);
        var totalServices = await _services.CountAsync(DocumentQuery.All, cancellationToken);
        var rpm = await _rpm.GetRequestsPerMinuteAsync(cancellationToken);
        return new SystemOverviewResponse((int)totalClients, (int)totalServices, rpm);
    }
}
