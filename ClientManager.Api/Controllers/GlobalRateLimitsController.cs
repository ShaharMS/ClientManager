using Asp.Versioning;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Shared.Models.Entities;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.Api.Controllers;

/// <summary>
/// Manages system-wide catch-all rate limits for services and resource pools.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/global-rate-limits")]
[Tags("Global Rate Limits")]
public class GlobalRateLimitsController(IGlobalRateLimitCatalogService globalRateLimitCatalogService)
    : CatalogCrudControllerBase<GlobalRateLimit>(globalRateLimitCatalogService);
