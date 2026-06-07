using ClientManager.Shared.Models.Entities;

namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Manages the system-wide catalog of standalone global rate-limit definitions.
/// Provides search and CRUD access over global rate-limit documents while keeping the public
/// controller surface decoupled from the persistence layer that backs the catalog.
/// </summary>
public interface IGlobalRateLimitCatalogService : ICatalogCrudService<GlobalRateLimit>;
