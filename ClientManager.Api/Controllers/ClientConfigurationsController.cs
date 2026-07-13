using ClientManager.Api.Services.Interfaces;
using ClientManager.Shared.Models.Entities;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.Api.Controllers;

/// <summary>
/// Manages client configuration documents and their top-level CRUD operations.
/// </summary>
/// <remarks>
/// <para>
/// A client configuration is the single source of truth for identity, enablement, global rate-limit
/// policy, and per-service access rules. The Admin UI loads and saves the full
/// <see cref="ClientConfiguration"/> so operators see the complete picture.
/// </para>
/// <para>
/// Search, create, update, and delete actions are inherited from
/// <see cref="CatalogCrudControllerBase{ClientConfiguration}"/> and power the Clients list and editor.
/// </para>
/// </remarks>
[ApiController]
[Route("api/v1/clients")]
[Tags("Client Configurations")]
public class ClientConfigurationsController(IClientConfigurationCatalogService catalog) : CatalogCrudControllerBase<ClientConfiguration>(catalog);
