using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Responses;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Handles client-configuration catalog CRUD.
/// </summary>
public interface IClientConfigurationCatalogService : ICatalogCrudService<ClientConfiguration>;
