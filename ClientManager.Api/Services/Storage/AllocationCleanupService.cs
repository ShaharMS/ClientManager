using System.Diagnostics;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.Shared.Configuration.Storage;
using ClientManager.Shared.Logging;
using ClientManager.Api.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace ClientManager.Api.Services.Storage;

/// <summary>
/// Periodically reconciles allocation counters and cleans up expired allocations.
/// </summary>
public class AllocationCleanupService : BackgroundService
{
    private readonly IAppLogger<AllocationCleanupService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BackgroundWorkersOptions _workerOptions;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(30);
    private bool _hasReconciled;

    public AllocationCleanupService(
        IAppLogger<AllocationCleanupService> logger,
        IServiceScopeFactory scopeFactory,
        IOptions<BackgroundWorkersOptions> workerOptions)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _workerOptions = workerOptions.Value;
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
                var leaderLock = scope.ServiceProvider.GetRequiredService<IDistributedLeaderLock>();
                await using var lease = await leaderLock.TryAcquireAsync("allocation-cleanup", stoppingToken);
                if (lease is null && _workerOptions.RequireLeaderLock)
                {
                    await Task.Delay(_interval, stoppingToken);
                    continue;
                }

                await ReconcileCountersAsync(scope.ServiceProvider, stoppingToken);

                var service = scope.ServiceProvider.GetRequiredService<IResourceAllocationService>();
                await service.CleanupExpiredAllocationsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.Error("Error cleaning up expired resource allocations", exception: exception);
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task ReconcileCountersAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        if (_hasReconciled)
        {
            return;
        }

        var allocationDatabase = serviceProvider.GetRequiredService<IResourceAllocationDatabase>();
        await allocationDatabase.ReconcileCountersAsync(cancellationToken);

        _hasReconciled = true;
        _logger.Info("Allocation counters reconciled on first cleanup cycle");
    }
}
