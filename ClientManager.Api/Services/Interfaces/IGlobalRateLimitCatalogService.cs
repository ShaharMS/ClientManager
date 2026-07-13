using ClientManager.Shared.Models.Entities;

namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Manages the system-wide catalog of standalone global rate-limit definitions.
/// </summary>
/// <remarks>
/// <para>
/// Provides search and CRUD access over global rate-limit documents while keeping the public
/// controller surface decoupled from the persistence layer that backs the catalog.
/// </para>
/// <para>
/// Each document defines the default policy for one service ID. Access checks consult these records
/// before per-client overrides.
/// </para>
/// </remarks>
public interface IGlobalRateLimitCatalogService : ICatalogCrudService<GlobalRateLimit>;
