using Asp.Versioning;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.Api.Controllers;

/// <summary>
/// Manages system-wide service definitions.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/services")]
[Tags("Services")]
public class ServicesController : ControllerBase
{
    private readonly IServiceCatalogService _serviceCatalogService;

    /// <summary>
    /// Initializes a new instance of <see cref="ServicesController"/>.
    /// </summary>
    /// <param name="serviceCatalogService">The service catalog service.</param>
    public ServicesController(IServiceCatalogService serviceCatalogService)
    {
        _serviceCatalogService = serviceCatalogService;
    }

    /// <summary>
    /// Searches services with optional filtering, sorting, and pagination.
    /// </summary>
    /// <param name="query">Query with filters, sort, and pagination. Pass an empty body or null for all results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching services and total count.</returns>
    /// <response code="200">Returns the matching services.</response>
    [HttpPost("search")]
    [ProducesResponseType(typeof(SearchResult<Service>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromBody] DocumentQuery? query,
        CancellationToken cancellationToken)
    {
        var services = await _serviceCatalogService.SearchAsync(query ?? DocumentQuery.All, cancellationToken);
        return Ok(services);
    }

    /// <summary>
    /// Retrieves a service by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the service.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The service.</returns>
    /// <response code="200">Returns the requested service.</response>
    /// <response code="404">No service was found with the given identifier.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Service), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        var service = await _serviceCatalogService.GetByIdAsync(id, cancellationToken);
        return Ok(service);
    }

    /// <summary>
    /// Creates a new service.
    /// </summary>
    /// <param name="service">The service to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created service.</returns>
    /// <response code="201">The service was created successfully.</response>
    /// <response code="409">A service with the same identifier already exists.</response>
    [HttpPost]
    [ProducesResponseType(typeof(Service), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] Service service, CancellationToken cancellationToken)
    {
        var created = await _serviceCatalogService.CreateAsync(service, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Updates an existing service.
    /// </summary>
    /// <param name="id">The unique identifier of the service to update.</param>
    /// <param name="service">The updated service.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated service.</returns>
    /// <response code="200">The service was updated successfully.</response>
    /// <response code="404">No service was found with the given identifier.</response>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(Service), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string id, [FromBody] Service service, CancellationToken cancellationToken)
    {
        var updated = await _serviceCatalogService.UpdateAsync(id, service, cancellationToken);
        return Ok(updated);
    }

    /// <summary>
    /// Deletes a service.
    /// </summary>
    /// <param name="id">The unique identifier of the service to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="204">The service was deleted successfully.</response>
    /// <response code="404">No service was found with the given identifier.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        await _serviceCatalogService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
