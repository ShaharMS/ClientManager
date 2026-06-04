using Asp.Versioning;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Shared.Models.Entities;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.Api.Controllers;

/// <summary>
/// Manages system-wide service definitions.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/services")]
[Tags("Services")]
public class ServicesController(IServiceCatalogService serviceCatalogService)
    : CatalogCrudControllerBase<Service>(serviceCatalogService);
