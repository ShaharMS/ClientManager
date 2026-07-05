using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Utils;
using ClientManager.Shared.Configuration.Storage;
using ClientManager.Shared.Logging;

namespace ClientManager.Api.Services.Storage;

/// <summary>
/// Seeds initial catalog data into persistence at API startup.
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
        var seedCatalogService = scope.ServiceProvider.GetRequiredService<ISeedCatalogService>();

        var summary = await seedCatalogService.ImportWithStrategyAsync(
            _seedOptions,
            SeedCollections.All,
            SeedImportStrategy.Skip,
            cancellationToken);

        _logger.Info("Data seeding completed", new
        {
            summary.Created,
            summary.Skipped
        });
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
