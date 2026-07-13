using ClientManager.Api.Services.Interfaces;

using ClientManager.Shared.Models.Entities;

using Microsoft.AspNetCore.Mvc;



namespace ClientManager.Api.Controllers;



[ApiController]

[Route("api/v1/clients")]

[Tags("Client Configurations")]

public class ClientConfigurationsController(IClientConfigurationCatalogService catalog) : CatalogCrudControllerBase<ClientConfiguration>(catalog);
