using Asp.Versioning;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;
using ClientManager.StorageApi.Models.Exceptions;
using ClientManager.StorageApi.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.StorageApi.Controllers;

/// <summary>
/// Manages internal resource-pool catalog endpoints.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("internal/v{version:apiVersion}/configuration/resource-pools")]
[Tags("Resource Pools")]
public class ResourcePoolsController : ControllerBase
{
    private readonly IResourcePoolCatalogService _service;

    public ResourcePoolsController(IResourcePoolCatalogService service)
    {
        _service = service;
    }

    /// <summary>
    /// Searches resource pools with optional filtering, sorting, and pagination.
    /// </summary>
    /// <param name="query">Query with filters, sort, and pagination. Pass an empty body or null for all results.</param>
    /// <response code="200">Returns the matching resource pools.</response>
    [HttpPost("search")]
    [ProducesResponseType(typeof(SearchResult<ResourcePool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromBody] DocumentQuery? query, CancellationToken cancellationToken)
    {
        var result = await _service.SearchAsync(query ?? DocumentQuery.All, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a resource pool by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the resource pool.</param>
    /// <response code="200">Returns the requested resource pool.</response>
    /// <response code="404">No resource pool was found with the given identifier.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ResourcePool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        var pool = await _service.GetByIdAsync(id, cancellationToken)
            ?? throw new ResourcePoolNotFoundException(id);
        return Ok(pool);
    }

    /// <summary>
    /// Creates a new resource pool.
    /// </summary>
    /// <param name="pool">The resource pool to create.</param>
    /// <response code="201">The resource pool was created successfully.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ResourcePool), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] ResourcePool pool, CancellationToken cancellationToken)
    {
        await _service.CreateAsync(pool, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { version = "1.0", id = pool.Id }, pool);
    }

    /// <summary>
    /// Updates an existing resource pool.
    /// </summary>
    /// <param name="id">The unique identifier of the resource pool to update.</param>
    /// <param name="pool">The updated resource pool.</param>
    /// <response code="200">The resource pool was updated successfully.</response>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ResourcePool), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(string id, [FromBody] ResourcePool pool, CancellationToken cancellationToken)
    {
        var updated = await _service.UpdateAsync(id, pool, cancellationToken);
        return Ok(updated);
    }

    /// <summary>
    /// Deletes a resource pool.
    /// </summary>
    /// <param name="id">The unique identifier of the resource pool to delete.</param>
    /// <response code="204">The resource pool was deleted successfully.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}