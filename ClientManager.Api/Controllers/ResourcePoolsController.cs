using ClientManager.Api.Models.Exceptions;
using ClientManager.DataAccess.Interfaces;
using ClientManager.Shared.Models.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.Api.Controllers;

/// <summary>
/// Manages system-wide resource pool definitions.
/// </summary>
[ApiController]
[Route("api/resource-pools")]
[Tags("Resource Pools")]
public class ResourcePoolsController : ControllerBase
{
    private readonly IEntityRepository<ResourcePool> _repository;

    /// <summary>
    /// Initializes a new instance of <see cref="ResourcePoolsController"/>.
    /// </summary>
    /// <param name="repository">The resource pool entity repository.</param>
    public ResourcePoolsController(IEntityRepository<ResourcePool> repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Lists all resource pools.
    /// </summary>
    /// <returns>A list of all resource pools.</returns>
    /// <response code="200">Returns all resource pools.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ResourcePool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var pools = await _repository.GetAllAsync(cancellationToken);
        return Ok(pools);
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
        var pool = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Resource pool '{id}' not found");
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
        await _repository.CreateAsync(pool, cancellationToken);
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
        await _repository.UpdateAsync(updated, cancellationToken);
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
        await _repository.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
