using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;
using StorageServiceCatalogService = ClientManager.Api.Services.Storage.Interfaces.IServiceCatalogService;

namespace ClientManager.Api.Services.Implementations;

/// <summary>
/// Adapts public service-catalog requests onto the in-process storage service catalog,
/// translating an absent service via <see cref="DomainErrors.Service"/> and reconciling
/// route identifiers on update so the controller never has to reshape the persisted document.
/// </summary>
public class ServiceCatalogService : IServiceCatalogService
{
    private readonly StorageServiceCatalogService _serviceCatalogService;

    /// <summary>
    /// Initializes a new instance of <see cref="ServiceCatalogService"/>.
    /// </summary>
    /// <param name="serviceCatalogService">In-process storage service catalog.</param>
    public ServiceCatalogService(StorageServiceCatalogService serviceCatalogService)
    {
        _serviceCatalogService = serviceCatalogService;
    }

    /// <inheritdoc />
    public Task<SearchResult<Service>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken = default) =>
        _serviceCatalogService.SearchAsync(query, cancellationToken);

    /// <inheritdoc />
    public async Task<Service> GetByIdAsync(string serviceId, CancellationToken cancellationToken = default) =>
        await _serviceCatalogService.GetByIdAsync(serviceId, cancellationToken)
            ?? throw DomainErrors.Service(serviceId);

    /// <inheritdoc />
    public async Task<Service> CreateAsync(Service service, CancellationToken cancellationToken = default)
    {
        await _serviceCatalogService.CreateAsync(service, cancellationToken);
        return service;
    }

    /// <inheritdoc />
    public Task<Service> UpdateAsync(string serviceId, Service service, CancellationToken cancellationToken = default) =>
        _serviceCatalogService.UpdateAsync(serviceId, service, cancellationToken);

    /// <inheritdoc />
    public Task DeleteAsync(string serviceId, CancellationToken cancellationToken = default) =>
        _serviceCatalogService.DeleteAsync(serviceId, cancellationToken);
}
