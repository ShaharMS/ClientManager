using ClientManager.Shared.Models.Entities;

namespace ClientManager.DataAccess.Databases.Interfaces;

/// <summary>
/// Database for <see cref="ClientConfiguration"/> documents. Provides full-document CRUD
/// plus fine-grained sub-document accessors for service and resource-pool settings.
///
/// <para><strong>Why sub-document helpers exist</strong></para>
/// <para>
///     A <see cref="ClientConfiguration"/> contains two nested dictionaries
///     (<see cref="ClientConfiguration.Services"/> and
///     <see cref="ClientConfiguration.ResourcePools"/>) that are frequently read and written
///     independently of the rest of the document - for example, granting a single client
///     access to a new service should not require replacing the entire configuration. The
///     <c>Get/Set/Remove</c> helpers for each dictionary entry let callers update one entry
///     at a time, which also reduces merge-conflict potential in concurrent scenarios.
/// </para>
///
/// <para><strong>Why this doesn't extend <c>IEntityRepository</c></strong></para>
/// <para>
///     The generic <see cref="Repositories.Interfaces.IEntityRepository{T}"/> assumes
///     simple flat documents with a string ID. <see cref="ClientConfiguration"/>'s
///     nested-dictionary shape and the need for sub-document operations make a standalone
///     interface clearer and more explicit about the available operations.
/// </para>
/// </summary>
public interface IClientConfigurationDatabase
{
    /// <summary>
    /// Retrieves a client configuration by its unique identifier.
    /// </summary>
    /// <param name="clientId">The unique identifier of the client.</param>
    /// <param name="cancellationToken">Cancels the lookup if the store is unresponsive.</param>
    /// <returns>The client configuration if found; otherwise <c>null</c>.</returns>
    Task<ClientConfiguration?> GetByIdAsync(string clientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all client configurations.
    /// </summary>
    /// <param name="cancellationToken">Cancels the enumeration early if the caller is shutting down.</param>
    /// <returns>A read-only list of all client configurations.</returns>
    Task<IReadOnlyList<ClientConfiguration>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new client configuration.
    /// </summary>
    /// <param name="configuration">The client configuration to create.</param>
    /// <param name="cancellationToken">Cancels the write before the document is persisted.</param>
    Task CreateAsync(ClientConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing client configuration.
    /// </summary>
    /// <param name="configuration">The client configuration with updated values.</param>
    /// <param name="cancellationToken">Cancels the update before it is persisted.</param>
    Task UpdateAsync(ClientConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a client configuration by its unique identifier.
    /// </summary>
    /// <param name="clientId">The unique identifier of the client to delete.</param>
    /// <param name="cancellationToken">Cancels the delete before it completes.</param>
    Task DeleteAsync(string clientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the service access settings for a specific service within a client configuration.
    /// </summary>
    /// <param name="clientId">The unique identifier of the client.</param>
    /// <param name="serviceId">The unique identifier of the service.</param>
    /// <param name="cancellationToken">Cancels the lookup if the store is unresponsive.</param>
    /// <returns>The service access settings if found; otherwise <c>null</c>.</returns>
    Task<ServiceAccessSettings?> GetServiceSettingsAsync(string clientId, string serviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the service access settings for a specific service within a client configuration.
    /// </summary>
    /// <param name="clientId">The unique identifier of the client.</param>
    /// <param name="serviceId">The unique identifier of the service.</param>
    /// <param name="settings">The service access settings to apply.</param>
    /// <param name="cancellationToken">Cancels the write before the sub-document update is persisted.</param>
    Task SetServiceSettingsAsync(string clientId, string serviceId, ServiceAccessSettings settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the service access settings for a specific service within a client configuration.
    /// </summary>
    /// <param name="clientId">The unique identifier of the client.</param>
    /// <param name="serviceId">The unique identifier of the service to remove settings for.</param>
    /// <param name="cancellationToken">Cancels the removal before it is persisted.</param>
    Task RemoveServiceSettingsAsync(string clientId, string serviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the resource pool settings for a specific pool within a client configuration.
    /// </summary>
    /// <param name="clientId">The unique identifier of the client.</param>
    /// <param name="resourcePoolId">The unique identifier of the resource pool.</param>
    /// <param name="cancellationToken">Cancels the lookup if the store is unresponsive.</param>
    /// <returns>The resource pool settings if found; otherwise <c>null</c>.</returns>
    Task<ResourcePoolSettings?> GetResourcePoolSettingsAsync(string clientId, string resourcePoolId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the resource pool settings for a specific pool within a client configuration.
    /// </summary>
    /// <param name="clientId">The unique identifier of the client.</param>
    /// <param name="resourcePoolId">The unique identifier of the resource pool.</param>
    /// <param name="settings">The resource pool settings to apply.</param>
    /// <param name="cancellationToken">Cancels the write before the sub-document update is persisted.</param>
    Task SetResourcePoolSettingsAsync(string clientId, string resourcePoolId, ResourcePoolSettings settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the resource pool settings for a specific pool within a client configuration.
    /// </summary>
    /// <param name="clientId">The unique identifier of the client.</param>
    /// <param name="resourcePoolId">The unique identifier of the resource pool to remove settings for.</param>
    /// <param name="cancellationToken">Cancels the removal before it is persisted.</param>
    Task RemoveResourcePoolSettingsAsync(string clientId, string resourcePoolId, CancellationToken cancellationToken = default);
}
