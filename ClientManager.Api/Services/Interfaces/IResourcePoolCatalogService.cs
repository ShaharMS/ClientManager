using ClientManager.Shared.Models.Entities;

namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Manages the system-wide catalog of resource pool definitions.
/// Provides search and CRUD access over resource pool documents while keeping the public
/// controller surface decoupled from the storage transport that backs the catalog.
/// </summary>
public interface IResourcePoolCatalogService : ICatalogCrudService<ResourcePool>;
