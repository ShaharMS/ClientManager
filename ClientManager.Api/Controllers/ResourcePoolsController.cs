using Asp.Versioning;
using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Services.InternalClients.Interfaces.Configuration;
using ClientManager.Shared.Models.Search;
using ClientManager.Shared.Models.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.Api.Controllers;

/// <summary>
/// Manages system-wide resource pool definitions.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/resource-pools")]
[Tags("Resource Pools")]
public class ResourcePoolsController : ControllerBase
{
    private readonly IResourcePoolCatalogClient _resourcePoolCatalogClient;

    /// <summary>
    /// Initializes a new instance of <see cref="ResourcePoolsController"/>.
    /// </summary>
    /// <param name="resourcePoolCatalogClient">The internal resource-pool catalog client.</param>
    public ResourcePoolsController(IResourcePoolCatalogClient resourcePoolCatalogClient)
    {
        _resourcePoolCatalogClient = resourcePoolCatalogClient;
    }

    /// <summary>
    /// Searches resource pools with optional filtering, sorting, and pagination.
    /// </summary>
    /// <param name="query">Query with filters, sort, and pagination. Pass an empty body or null for all results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching resource pools and total count.</returns>
    /// <response code="200">Returns the matching resource pools.</response>
    [HttpPost("search")]
    [ProducesResponseType(typeof(SearchResult<ResourcePool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromBody] DocumentQuery? query,
        CancellationToken cancellationToken)
    {
        var result = await _resourcePoolCatalogClient.SearchAsync(query ?? DocumentQuery.All, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a resource pool by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the resource pool.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resource pool.</returns>
    /// <response code="200">Returns the requested resource pool.</response>
    /// <response code="404">No resource pool was found with the given identifier.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ResourcePool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        var pool = await _resourcePoolCatalogClient.GetByIdAsync(id, cancellationToken)
            ?? throw new ResourcePoolNotFoundException(id);
        return Ok(pool);
    }

    /// <summary>
    /// Creates a new resource pool.
    /// </summary>
    /// <param name="pool">The resource pool to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created resource pool.</returns>
    /// <response code="201">The resource pool was created successfully.</response>
    /// <response code="409">A resource pool with the same identifier already exists.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ResourcePool), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] ResourcePool pool, CancellationToken cancellationToken)
    {
        await _resourcePoolCatalogClient.CreateAsync(pool, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = pool.Id }, pool);
    }

    /// <summary>
    /// Updates an existing resource pool.
    /// </summary>
    /// <param name="id">The unique identifier of the resource pool to update.</param>
    /// <param name="pool">The updated resource pool.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated resource pool.</returns>
    /// <response code="200">The resource pool was updated successfully.</response>
    /// <response code="404">No resource pool was found with the given identifier.</response>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ResourcePool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string id, [FromBody] ResourcePool pool, CancellationToken cancellationToken)
    {
        var updated = pool with { Id = id };
        await _resourcePoolCatalogClient.UpdateAsync(id, pool, cancellationToken);
        return Ok(updated);
    }

    /// <summary>
    /// Deletes a resource pool.
    /// </summary>
    /// <param name="id">The unique identifier of the resource pool to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="204">The resource pool was deleted successfully.</response>
    /// <response code="404">No resource pool was found with the given identifier.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        await _resourcePoolCatalogClient.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
