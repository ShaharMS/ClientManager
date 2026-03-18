using System.Diagnostics;
using ClientManager.Api.Interfaces;

namespace ClientManager.Api.Services;

/// <summary>
/// Background service that periodically cleans up expired resource allocations.
/// </summary>
public class AllocationCleanupService : BackgroundService
{
    private readonly ILogger<AllocationCleanupService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Initializes a new instance of <see cref="AllocationCleanupService"/>.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="scopeFactory">Factory for creating service scopes.</param>
    public AllocationCleanupService(
        ILogger<AllocationCleanupService> logger,
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
                var service = scope.ServiceProvider.GetRequiredService<IResourceAllocationService>();
                await service.CleanupExpiredAllocationsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up expired resource allocations");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }
}
