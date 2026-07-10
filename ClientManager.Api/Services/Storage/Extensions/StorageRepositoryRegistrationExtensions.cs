using ClientManager.DataAccess.Databases.Implementations;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.DataAccess.Repositories.Implementations;
using ClientManager.DataAccess.Repositories.Interfaces;
using ClientManager.DataAccess.Stores.Interfaces;
using ClientManager.Shared.Configuration.Storage;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using Microsoft.Extensions.Options;

namespace ClientManager.Api.Services.Storage.Extensions;

/// <summary>
/// Registers database and repository abstractions on top of keyed document stores.
/// </summary>
public static class StorageRepositoryRegistrationExtensions
{
    /// <summary>
    /// Adds repository and database registrations for the in-process persistence layer.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddStorageRepositories(this IServiceCollection services)
    {
        services.AddSingleton<IClientConfigurationDatabase>(sp =>
            new ClientConfigurationDatabase(
                sp.GetRequiredKeyedService<IDocumentStore>(StorageRole.Configuration)));

        services.AddSingleton<IEntityRepository<Service>>(sp =>
            new EntityRepository<Service>(
                sp.GetRequiredKeyedService<IDocumentStore>(StorageRole.Configuration),
                "services",
                service => service.Id));

        services.AddSingleton<IEntityRepository<ResourcePool>>(sp =>
            new EntityRepository<ResourcePool>(
                sp.GetRequiredKeyedService<IDocumentStore>(StorageRole.Configuration),
                "resource_pools",
                pool => pool.Id));

        services.AddSingleton<IGlobalRateLimitDatabase>(sp =>
            new GlobalRateLimitDatabase(
                sp.GetRequiredKeyedService<IDocumentStore>(StorageRole.Configuration)));

        services.AddSingleton<IRateLimitStateDatabase>(sp =>
            new RateLimitStateDatabase(
                sp.GetRequiredKeyedService<IDocumentStore>(StorageRole.RateLimiting)));

        services.AddSingleton<IResourceAllocationDatabase>(sp =>
            new ResourceAllocationDatabase(
                sp.GetRequiredKeyedService<IDocumentStore>(StorageRole.Allocations)));

        services.AddSingleton<IStatisticsPrecomputedDatabase>(sp =>
            new StatisticsPrecomputedDatabase(
                sp.GetRequiredKeyedService<IDocumentStore>(StorageRole.Statistics)));

        services.AddSingleton<IUsageSnapshotDatabase>(sp =>
        {
            var persistence = sp.GetRequiredService<IOptions<PersistenceOptions>>().Value;
            var statisticsBinding = ResolveStatisticsBinding(persistence);
            var clientDb = sp.GetRequiredService<IClientConfigurationDatabase>();

            if (statisticsBinding.Provider == PersistenceProvider.Sqlite)
            {
                var databasePath = statisticsBinding.Sqlite?.DatabasePath ?? "./data/statistics.db";
                return new SqliteUsageSnapshotDatabase(databasePath, clientDb);
            }

            return new UsageSnapshotDatabase(
                sp.GetRequiredKeyedService<IDocumentStore>(StorageRole.Statistics),
                clientDb);
        });

        return services;
    }

    private static StorageRoleBinding ResolveStatisticsBinding(PersistenceOptions persistence)
    {
        if (persistence.Roles?.TryGetValue(StorageRole.Statistics, out var binding) == true)
        {
            return binding;
        }

        return new StorageRoleBinding
        {
            Provider = persistence.DefaultProvider,
            MongoDb = persistence.DefaultMongoDb,
            Redis = persistence.DefaultRedis,
            JsonFile = persistence.DefaultJsonFile,
            Lucene = persistence.DefaultLucene,
            Sqlite = persistence.DefaultSqlite
        };
    }
}
