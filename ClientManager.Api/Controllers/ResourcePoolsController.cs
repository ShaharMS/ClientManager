using Asp.Versioning;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Shared.Models.Entities;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.Api.Controllers;

/// <summary>
/// Manages system-wide resource pool definitions.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/resource-pools")]
[Tags("Resource Pools")]
public class ResourcePoolsController(IResourcePoolCatalogService resourcePoolCatalogService)
    : CatalogCrudControllerBase<ResourcePool>(resourcePoolCatalogService);
