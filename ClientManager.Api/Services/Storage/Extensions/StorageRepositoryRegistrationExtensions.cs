using ClientManager.DataAccess.Databases.Implementations;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.DataAccess.Repositories.Implementations;
using ClientManager.DataAccess.Repositories.Interfaces;
using ClientManager.DataAccess.Stores.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;

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

        services.AddSingleton<IUsageSnapshotDatabase>(sp =>
            new UsageSnapshotDatabase(
                sp.GetRequiredKeyedService<IDocumentStore>(StorageRole.Statistics),
                sp.GetRequiredService<IClientConfigurationDatabase>()));

        return services;
    }
}