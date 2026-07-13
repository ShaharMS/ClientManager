using ClientManager.Shared.Models.Entities;

namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Manages the system-wide catalog of service definitions.
/// </summary>
/// <remarks>
/// <para>
/// Provides search and CRUD access over service documents while keeping the public controller
/// surface decoupled from the persistence layer that backs the catalog.
/// </para>
/// <para>
/// Service records supply the identifiers and enablement flags used by access checks, rate limits,
/// and observability labels.
/// </para>
/// </remarks>
public interface IServiceCatalogService : ICatalogCrudService<Service>;
