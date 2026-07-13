using ClientManager.Shared.Models.Entities;

namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Handles client-configuration catalog CRUD.
/// </summary>
/// <remarks>
/// <para>
/// Client configurations are the richest catalog documents: identity, enablement, global rate-limit
/// policy, and per-service access rules live in one JSON document. The Admin UI always loads and
/// saves the full <see cref="ClientConfiguration"/> so operators see the complete picture.
/// </para>
/// <para>
/// This marker interface keeps dependency injection and Swagger grouping explicit while inheriting
/// the shared search/create/update/delete contract from <see cref="ICatalogCrudService{ClientConfiguration}"/>.
/// </para>
/// </remarks>
public interface IClientConfigurationCatalogService : ICatalogCrudService<ClientConfiguration>;
