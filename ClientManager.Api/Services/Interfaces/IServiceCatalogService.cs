using ClientManager.Shared.Models.Entities;

namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Manages the system-wide catalog of service definitions.
/// Provides search and CRUD access over service documents while keeping the public
/// controller surface decoupled from the persistence layer that backs the catalog.
/// </summary>
public interface IServiceCatalogService : ICatalogCrudService<Service>;
