using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.Shared.Configuration.Storage;
using ClientManager.Shared.Logging;
using Microsoft.Extensions.Options;

namespace ClientManager.Api.Services.Storage;

/// <summary>
/// Reconciles stale <c>usage:</c> pending counters once on startup (mirrors allocation counter reconcile).
/// </summary>
public sealed class UsageCounterCleanupService : BackgroundService
{
    private readonly IAppLogger<UsageCounterCleanupService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _retention;

    public UsageCounterCleanupService(
        IAppLogger<UsageCounterCleanupService> logger,
        IServiceScopeFactory scopeFactory,
        IOptions<UsageTrackingOptions> usageTrackingOptions)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _retention = usageTrackingOptions.Value.SecondRetention;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<IUsageSnapshotDatabase>();
            var purged = await database.ReconcileUsageCountersAsync(_retention, stoppingToken);
            if (purged > 0)
            {
                _logger.Info("Reconciled stale usage counters on startup", new { Purged = purged });
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            _logger.Error("Error reconciling usage counters on startup", exception: exception);
        }

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
