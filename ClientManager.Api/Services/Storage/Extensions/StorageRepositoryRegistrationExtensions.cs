using ClientManager.Api.Storage.Databases.Implementations;
using ClientManager.Api.Storage.Databases.Interfaces;
using ClientManager.Api.Storage.Repositories.Implementations;
using ClientManager.Api.Storage.Repositories.Interfaces;
using ClientManager.Api.Storage.Stores.Interfaces;
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
    public static IServiceCollection AddStorageRepositories(this IServiceCollection services)
    {
        services.AddSingleton<IClientConfigurationDatabase>(sp =>
            new ClientConfigurationDatabase(sp.GetRequiredKeyedService<IDocumentStore>(StorageRole.Configuration)));

        services.AddSingleton<IEntityRepository<Service>>(sp =>
            new EntityRepository<Service>(
                sp.GetRequiredKeyedService<IDocumentStore>(StorageRole.Configuration),
                "services",
                service => service.Id));

        services.AddSingleton<IEntityRepository<GlobalRateLimit>>(sp =>
            new EntityRepository<GlobalRateLimit>(
                sp.GetRequiredKeyedService<IDocumentStore>(StorageRole.Configuration),
                "GlobalRateLimit",
                limit => limit.Id));

        services.AddSingleton<IGlobalRateLimitDatabase>(sp =>
            new GlobalRateLimitDatabase(sp.GetRequiredKeyedService<IDocumentStore>(StorageRole.Configuration)));

        services.AddSingleton<IRateLimitStateDatabase>(sp =>
            new RateLimitStateDatabase(sp.GetRequiredKeyedService<IDocumentStore>(StorageRole.RateLimiting)));

        services.AddSingleton<IRpmRingDatabase>(sp =>
            new RpmRingDatabase(sp.GetRequiredKeyedService<IDocumentStore>(StorageRole.Rpm)));

        return services;
    }
}
