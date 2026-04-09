using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.DataAccess.Repositories.Interfaces;
using ClientManager.Shared.Logging;
using ClientManager.Shared.Models.Entities;
using ClientManager.StorageApi.Models.Configuration;

namespace ClientManager.StorageApi.Services.Implementations;

/// <summary>
/// Seeds initial catalog data into the storage-owned persistence boundary.
/// </summary>
public class DataSeedService : IHostedService
{
    private readonly IAppLogger<DataSeedService> _logger;
    private readonly SeedOptions _seedOptions;
    private readonly IServiceScopeFactory _scopeFactory;

    public DataSeedService(
        IAppLogger<DataSeedService> logger,
        SeedOptions seedOptions,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _seedOptions = seedOptions;
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var clientConfigDb = scope.ServiceProvider.GetRequiredService<IClientConfigurationDatabase>();
        var serviceRepo = scope.ServiceProvider.GetRequiredService<IEntityRepository<Service>>();
        var poolRepo = scope.ServiceProvider.GetRequiredService<IEntityRepository<ResourcePool>>();
        var globalRateLimitDb = scope.ServiceProvider.GetRequiredService<IGlobalRateLimitDatabase>();

        await SeedServicesAsync(serviceRepo, cancellationToken);
        await SeedResourcePoolsAsync(poolRepo, cancellationToken);
        await SeedGlobalRateLimitsAsync(globalRateLimitDb, cancellationToken);
        await SeedClientConfigurationsAsync(clientConfigDb, cancellationToken);

        _logger.Info("Data seeding completed");
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedServicesAsync(
        IEntityRepository<Service> serviceRepository,
        CancellationToken cancellationToken)
    {
        foreach (var service in _seedOptions.Services)
        {
            if (await serviceRepository.GetByIdAsync(service.Id, cancellationToken) is not null)
            {
                continue;
            }

            await serviceRepository.CreateAsync(service, cancellationToken);
            _logger.Info("Seeded service", new { ServiceId = service.Id });
        }
    }

    private async Task SeedResourcePoolsAsync(
        IEntityRepository<ResourcePool> resourcePoolRepository,
        CancellationToken cancellationToken)
    {
        foreach (var pool in _seedOptions.ResourcePools)
        {
            if (await resourcePoolRepository.GetByIdAsync(pool.Id, cancellationToken) is not null)
            {
                continue;
            }

            await resourcePoolRepository.CreateAsync(pool, cancellationToken);
            _logger.Info("Seeded resource pool", new { ResourcePoolId = pool.Id });
        }
    }

    private async Task SeedGlobalRateLimitsAsync(
        IGlobalRateLimitDatabase globalRateLimitDatabase,
        CancellationToken cancellationToken)
    {
        foreach (var limit in _seedOptions.GlobalRateLimits)
        {
            if (await globalRateLimitDatabase.GetByIdAsync(limit.Id, cancellationToken) is not null)
            {
                continue;
            }

            await globalRateLimitDatabase.CreateAsync(limit, cancellationToken);
            _logger.Info("Seeded global rate limit", new { GlobalRateLimitId = limit.Id });
        }
    }

    private async Task SeedClientConfigurationsAsync(
        IClientConfigurationDatabase clientConfigurationDatabase,
        CancellationToken cancellationToken)
    {
        foreach (var configuration in _seedOptions.ClientConfigurations)
        {
            if (await clientConfigurationDatabase.GetByIdAsync(configuration.Id, cancellationToken) is not null)
            {
                continue;
            }

            await clientConfigurationDatabase.CreateAsync(configuration, cancellationToken);
            _logger.Info("Seeded client configuration", new { ClientId = configuration.Id });
        }
    }
}