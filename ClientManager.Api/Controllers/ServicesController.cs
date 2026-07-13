using ClientManager.Api.Services.Interfaces;
using ClientManager.Shared.Models.Entities;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.Api.Controllers;

[ApiController]
[Route("api/v1/services")]
[Tags("Services")]
public class ServicesController(IServiceCatalogService catalog) : CatalogCrudControllerBase<Service>(catalog);
