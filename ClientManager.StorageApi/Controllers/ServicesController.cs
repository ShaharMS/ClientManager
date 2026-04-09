using Asp.Versioning;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;
using ClientManager.StorageApi.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.StorageApi.Controllers;

/// <summary>
/// Manages internal service-definition catalog endpoints.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("internal/v{version:apiVersion}/configuration/services")]
[Tags("Services")]
public class ServicesController : ControllerBase
{
    private readonly IServiceCatalogService _service;

    public ServicesController(IServiceCatalogService service)
    {
        _service = service;
    }

    /// <summary>
    /// Searches services with optional filtering, sorting, and pagination.
    /// </summary>
    /// <param name="query">Query with filters, sort, and pagination. Pass an empty body or null for all results.</param>
    /// <response code="200">Returns the matching services.</response>
    [HttpPost("search")]
    [ProducesResponseType(typeof(SearchResult<Service>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromBody] DocumentQuery? query, CancellationToken cancellationToken)
    {
        var result = await _service.SearchAsync(query ?? DocumentQuery.All, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a service by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the service.</param>
    /// <response code="200">Returns the requested service.</response>
    /// <response code="404">No service was found with the given identifier.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Service), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        var service = await _service.GetByIdAsync(id, cancellationToken);
        return service is null ? NotFound() : Ok(service);
    }

    /// <summary>
    /// Creates a new service.
    /// </summary>
    /// <param name="service">The service to create.</param>
    /// <response code="201">The service was created successfully.</response>
    [HttpPost]
    [ProducesResponseType(typeof(Service), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] Service service, CancellationToken cancellationToken)
    {
        await _service.CreateAsync(service, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { version = "1.0", id = service.Id }, service);
    }

    /// <summary>
    /// Updates an existing service.
    /// </summary>
    /// <param name="id">The unique identifier of the service to update.</param>
    /// <param name="service">The updated service.</param>
    /// <response code="200">The service was updated successfully.</response>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(Service), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(string id, [FromBody] Service service, CancellationToken cancellationToken)
    {
        var updated = await _service.UpdateAsync(id, service, cancellationToken);
        return Ok(updated);
    }

    /// <summary>
    /// Deletes a service.
    /// </summary>
    /// <param name="id">The unique identifier of the service to delete.</param>
    /// <response code="204">The service was deleted successfully.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}