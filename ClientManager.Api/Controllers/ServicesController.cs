using ClientManager.Api.Services.Interfaces;
using ClientManager.Shared.Models.Entities;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.Api.Controllers;

/// <summary>
/// Manages system-wide service definitions.
/// </summary>
/// <remarks>
/// <para>
/// Services are the endpoints that clients may call. Each record controls availability (enabled/disabled)
/// and supplies the identifier used by access checks, rate limits, and observability labels.
/// </para>
/// <para>
/// CRUD behavior is inherited from <see cref="CatalogCrudControllerBase{Service}"/> and backs the Services
/// list and editor in the Admin UI.
/// </para>
/// </remarks>
[ApiController]
[Route("api/v2/services")]
[Tags("Services")]
public class ServicesController(IServiceCatalogService catalog) : CatalogCrudControllerBase<Service>(catalog);
