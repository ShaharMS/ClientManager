using ClientManager.Api.Services.Interfaces;
using ClientManager.Shared.Models.Entities;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.Api.Controllers;

[ApiController]
[Route("api/v1/global-rate-limits")]
[Tags("Global Rate Limits")]
public class GlobalRateLimitsController(IGlobalRateLimitCatalogService catalog) : CatalogCrudControllerBase<GlobalRateLimit>(catalog);
