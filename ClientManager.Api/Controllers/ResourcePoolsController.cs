using Asp.Versioning;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Shared.Models.Search;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Problems;
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
    private readonly IResourcePoolCatalogService _resourcePoolCatalogService;

    /// <summary>
    /// Initializes a new instance of <see cref="ResourcePoolsController"/>.
    /// </summary>
    /// <param name="resourcePoolCatalogService">The resource-pool catalog service.</param>
    public ResourcePoolsController(IResourcePoolCatalogService resourcePoolCatalogService)
    {
        _resourcePoolCatalogService = resourcePoolCatalogService;
    }

    /// <summary>
    /// Searches resource pools with optional filtering, sorting, and pagination.
    /// </summary>
    /// <param name="query">Query with filters, sort, and pagination. Pass an empty body or null for all results.</param>
    /// <param name="cancellationToken">Token used to cancel the resource pool search before it completes.</param>
    /// <returns>Matching resource pools and total count.</returns>
    /// <response code="200">Returns the matching resource pools.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpPost("search")]
    [ProducesResponseType(typeof(SearchResult<ResourcePool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Search(
        [FromBody] DocumentQuery? query,
        CancellationToken cancellationToken)
    {
        var pools = await _resourcePoolCatalogService.SearchAsync(query ?? DocumentQuery.All, cancellationToken);
        return Ok(pools);
    }

    /// <summary>
    /// Retrieves a resource pool by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the resource pool.</param>
    /// <param name="cancellationToken">Token used to cancel the resource pool lookup before it completes.</param>
    /// <returns>The resource pool.</returns>
    /// <response code="200">Returns the requested resource pool.</response>
    /// <response code="404">No resource pool was found with the given identifier.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ResourcePool), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        var pool = await _resourcePoolCatalogService.GetByIdAsync(id, cancellationToken);
        return Ok(pool);
    }

    /// <summary>
    /// Creates a new resource pool.
    /// </summary>
    /// <param name="pool">The resource pool to create.</param>
    /// <param name="cancellationToken">Token used to abort the create-resource-pool request before it is persisted.</param>
    /// <returns>The created resource pool.</returns>
    /// <response code="201">The resource pool was created successfully.</response>
    /// <response code="409">A resource pool with the same identifier already exists.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ResourcePool), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Create([FromBody] ResourcePool pool, CancellationToken cancellationToken)
    {
        var created = await _resourcePoolCatalogService.CreateAsync(pool, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Updates an existing resource pool.
    /// </summary>
    /// <param name="id">The unique identifier of the resource pool to update.</param>
    /// <param name="pool">The updated resource pool.</param>
    /// <param name="cancellationToken">Token used to abort the resource pool update before it is persisted.</param>
    /// <returns>The updated resource pool.</returns>
    /// <response code="200">The resource pool was updated successfully.</response>
    /// <response code="404">No resource pool was found with the given identifier.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ResourcePool), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Update(string id, [FromBody] ResourcePool pool, CancellationToken cancellationToken)
    {
        var updated = await _resourcePoolCatalogService.UpdateAsync(id, pool, cancellationToken);
        return Ok(updated);
    }

    /// <summary>
    /// Deletes a resource pool.
    /// </summary>
    /// <param name="id">The unique identifier of the resource pool to delete.</param>
    /// <param name="cancellationToken">Token used to abort the resource pool deletion before it completes.</param>
    /// <response code="204">The resource pool was deleted successfully.</response>
    /// <response code="404">No resource pool was found with the given identifier.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        await _resourcePoolCatalogService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
