using ClientManager.Api.Models;
using ClientManager.DataAccess.Interfaces;
using ClientManager.Shared.Models.Entities;

namespace ClientManager.Api.Services;

/// <summary>
/// A one-shot hosted service that populates the persistence layer with initial data on application startup.
/// <para>
/// Reads from <see cref="SeedOptions"/> (bound from the <c>"Seed"</c> section of <c>appsettings.json</c>)
/// and creates any entities that don't already exist. This allows operators to define a baseline set of
/// services, resource pools, global rate limits, and client configurations in config so the system
/// is ready immediately after first deployment—no manual API calls required.
/// </para>
/// <para>
/// Seed data is idempotent: entities are checked by ID before creation, so restarting the application
/// will not duplicate previously seeded data.
/// </para>
/// </summary>
public class DataSeedService : IHostedService
{
    private readonly ILogger<DataSeedService> _logger;
    private readonly SeedOptions _seedOptions;
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    /// Initializes a new instance of <see cref="DataSeedService"/>.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="seedOptions">The seed data configuration.</param>
    /// <param name="scopeFactory">Factory for creating service scopes.</param>
    public DataSeedService(
        ILogger<DataSeedService> logger,
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

        var clientConfigRepo = scope.ServiceProvider.GetRequiredService<IClientConfigurationRepository>();
        var serviceRepo = scope.ServiceProvider.GetRequiredService<IEntityRepository<Service>>();
        var poolRepo = scope.ServiceProvider.GetRequiredService<IEntityRepository<ResourcePool>>();
        var globalRateLimitRepo = scope.ServiceProvider.GetRequiredService<IGlobalRateLimitRepository>();

        foreach (var service in _seedOptions.Services)
        {
            var existing = await serviceRepo.GetByIdAsync(service.Id, cancellationToken);
            if (existing is null)
            {
                await serviceRepo.CreateAsync(service, cancellationToken);
                _logger.LogInformation("Seeded service | ServiceId={ServiceId}", service.Id);
            }
        }

        foreach (var pool in _seedOptions.ResourcePools)
        {
            var existing = await poolRepo.GetByIdAsync(pool.Id, cancellationToken);
            if (existing is null)
            {
                await poolRepo.CreateAsync(pool, cancellationToken);
                _logger.LogInformation("Seeded resource pool | ResourcePoolId={ResourcePoolId}", pool.Id);
            }
        }

        foreach (var globalLimit in _seedOptions.GlobalRateLimits)
        {
            var existing = await globalRateLimitRepo.GetByIdAsync(globalLimit.Id, cancellationToken);
            if (existing is null)
            {
                await globalRateLimitRepo.CreateAsync(globalLimit, cancellationToken);
                _logger.LogInformation("Seeded global rate limit | GlobalRateLimitId={GlobalRateLimitId}", globalLimit.Id);
            }
        }

        foreach (var config in _seedOptions.ClientConfigurations)
        {
            var existing = await clientConfigRepo.GetByIdAsync(config.Id, cancellationToken);
            if (existing is null)
            {
                await clientConfigRepo.CreateAsync(config, cancellationToken);
                _logger.LogInformation("Seeded client configuration | ClientId={ClientId}", config.Id);
            }
        }

        _logger.LogInformation("Data seeding completed");
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
