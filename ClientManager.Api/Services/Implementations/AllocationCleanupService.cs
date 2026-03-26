using System.Diagnostics;

using ClientManager.Api.Services.Interfaces;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.Shared.Logging;

namespace ClientManager.Api.Services.Implementations;

/// <summary>
/// Background service that periodically cleans up expired resource allocations.
/// <para>
/// Runs on a 30-second interval. On the first cycle, reconciles the atomic allocation
/// counters to correct any drift caused by unexpected shutdowns. Subsequent cycles
/// delegate to <see cref="IResourceAllocationService.CleanupExpiredAllocationsAsync"/>
/// to reclaim slots whose TTL has elapsed.
/// </para>
/// </summary>
public class AllocationCleanupService : BackgroundService
{
    private readonly IAppLogger<AllocationCleanupService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(30);
    private bool _hasReconciled;

    /// <summary>
    /// Initializes a new instance of <see cref="AllocationCleanupService"/>.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="scopeFactory">Factory for creating service scopes.</param>
    public AllocationCleanupService(
        IAppLogger<AllocationCleanupService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var activity = new Activity("AllocationCleanup").Start();

            try
            {
                using var scope = _scopeFactory.CreateScope();

                if (!_hasReconciled)
                {
                    var allocationDatabase = scope.ServiceProvider.GetRequiredService<IResourceAllocationDatabase>();
                    await allocationDatabase.ReconcileCountersAsync(stoppingToken);
                    _hasReconciled = true;
                    _logger.Info("Allocation counters reconciled on first cleanup cycle");
                }

                var service = scope.ServiceProvider.GetRequiredService<IResourceAllocationService>();
                await service.CleanupExpiredAllocationsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Error("Error cleaning up expired resource allocations", ex);
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }
}
