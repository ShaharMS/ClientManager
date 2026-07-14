using ClientManager.Api.Services.Interfaces;
using ClientManager.Shared.Models.Entities;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.Api.Controllers;

/// <summary>
/// Manages system-wide catch-all rate limits applied to services.
/// </summary>
/// <remarks>
/// <para>
/// Each record defines the default rate-limit policy for one service ID. These limits apply across all
/// clients unless a client is exempt or has a more specific per-service override inside its configuration.
/// </para>
/// <para>
/// CRUD behavior is inherited from <see cref="CatalogCrudControllerBase{GlobalRateLimit}"/> and is used
/// by the Rate Limits pages in the Admin UI.
/// </para>
/// </remarks>
[ApiController]
[Route("api/v2/global-rate-limits")]
[Tags("Global Rate Limits")]
public class GlobalRateLimitsController(IGlobalRateLimitCatalogService catalog) : CatalogCrudControllerBase<GlobalRateLimit>(catalog);
