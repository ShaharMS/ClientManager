using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Services.Internal.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services.Implementations;

/// <summary>
/// Adapts public service-catalog requests onto the storage-facing
/// <see cref="IServiceCatalogClient"/>, reconciling route identifiers on update so the
/// controller never has to reshape the persisted document.
/// </summary>
public class ServiceCatalogService : IServiceCatalogService
{
    private readonly IServiceCatalogClient _serviceCatalogClient;

    /// <summary>
    /// Initializes a new instance of <see cref="ServiceCatalogService"/>.
    /// </summary>
    /// <param name="serviceCatalogClient">Typed client for the storage-facing service catalog.</param>
    public ServiceCatalogService(IServiceCatalogClient serviceCatalogClient)
    {
        _serviceCatalogClient = serviceCatalogClient;
    }

    /// <inheritdoc />
    public Task<SearchResult<Service>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken = default) =>
        _serviceCatalogClient.SearchAsync(query, cancellationToken);

    /// <inheritdoc />
    public Task<Service> GetByIdAsync(string serviceId, CancellationToken cancellationToken = default) =>
        _serviceCatalogClient.GetByIdAsync(serviceId, cancellationToken);

    /// <inheritdoc />
    public async Task<Service> CreateAsync(Service service, CancellationToken cancellationToken = default)
    {
        await _serviceCatalogClient.CreateAsync(service, cancellationToken);
        return service;
    }

    /// <inheritdoc />
    public async Task<Service> UpdateAsync(string serviceId, Service service, CancellationToken cancellationToken = default)
    {
        await _serviceCatalogClient.UpdateAsync(serviceId, service, cancellationToken);
        return service with { Id = serviceId };
    }

    /// <inheritdoc />
    public Task DeleteAsync(string serviceId, CancellationToken cancellationToken = default) =>
        _serviceCatalogClient.DeleteAsync(serviceId, cancellationToken);
}
