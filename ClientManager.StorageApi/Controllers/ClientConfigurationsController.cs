using Asp.Versioning;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;
using ClientManager.StorageApi.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.StorageApi.Controllers;

/// <summary>
/// Manages internal client-configuration CRUD and search endpoints.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("internal/v{version:apiVersion}/configuration/clients")]
[Tags("Client Configurations")]
public class ClientConfigurationsController : ControllerBase
{
    private readonly IClientConfigurationCatalogService _service;

    public ClientConfigurationsController(IClientConfigurationCatalogService service)
    {
        _service = service;
    }

    /// <summary>
    /// Searches client configurations with optional filtering, sorting, and pagination.
    /// </summary>
    /// <param name="query">Query with filters, sort, and pagination. Pass an empty body or null for all results.</param>
    /// <response code="200">Returns the matching client configurations.</response>
    [HttpPost("search")]
    [ProducesResponseType(typeof(SearchResult<ClientConfiguration>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromBody] DocumentQuery? query, CancellationToken cancellationToken)
    {
        var result = await _service.SearchAsync(query ?? DocumentQuery.All, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a client configuration by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <response code="200">Returns the requested client configuration.</response>
    /// <response code="404">No client was found with the given identifier.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ClientConfiguration), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        var config = await _service.GetByIdAsync(id, cancellationToken);
        return config is null ? NotFound() : Ok(config);
    }

    /// <summary>
    /// Creates a new client configuration.
    /// </summary>
    /// <param name="configuration">The client configuration to create.</param>
    /// <response code="201">The client configuration was created successfully.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ClientConfiguration), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] ClientConfiguration configuration, CancellationToken cancellationToken)
    {
        await _service.CreateAsync(configuration, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { version = "1.0", id = configuration.Id }, configuration);
    }

    /// <summary>
    /// Updates an existing client configuration.
    /// </summary>
    /// <param name="id">The unique identifier of the client to update.</param>
    /// <param name="configuration">The updated client configuration.</param>
    /// <response code="200">The client configuration was updated successfully.</response>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ClientConfiguration), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(string id, [FromBody] ClientConfiguration configuration, CancellationToken cancellationToken)
    {
        var updated = await _service.UpdateAsync(id, configuration, cancellationToken);
        return Ok(updated);
    }

    /// <summary>
    /// Deletes a client configuration.
    /// </summary>
    /// <param name="id">The unique identifier of the client to delete.</param>
    /// <response code="204">The client configuration was deleted successfully.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}