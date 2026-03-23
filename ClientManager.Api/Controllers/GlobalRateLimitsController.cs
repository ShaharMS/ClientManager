using ClientManager.Api.Models.Exceptions;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.Api.Controllers;

/// <summary>
/// Manages system-wide catch-all rate limits for services and resource pools.
/// </summary>
[ApiController]
[Route("api/global-rate-limits")]
[Tags("Global Rate Limits")]
public class GlobalRateLimitsController : ControllerBase
{
    private readonly IGlobalRateLimitRepository _repository;

    /// <summary>
    /// Initializes a new instance of <see cref="GlobalRateLimitsController"/>.
    /// </summary>
    /// <param name="repository">The global rate limit repository.</param>
    public GlobalRateLimitsController(IGlobalRateLimitRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Lists all global rate limits, optionally filtered by target type.
    /// </summary>
    /// <param name="targetType">Optional filter by target type (Service or ResourcePool).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of global rate limits.</returns>
    /// <response code="200">Returns the global rate limits.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<GlobalRateLimit>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] TargetType? targetType, CancellationToken cancellationToken)
    {
        if (targetType.HasValue)
        {
            var filtered = await _repository.GetByTargetTypeAsync(targetType.Value, cancellationToken);
            return Ok(filtered);
        }

        var all = await _repository.GetAllAsync(cancellationToken);
        return Ok(all);
    }

    /// <summary>
    /// Retrieves a global rate limit by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the global rate limit.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The global rate limit.</returns>
    /// <response code="200">Returns the requested global rate limit.</response>
    /// <response code="404">No global rate limit was found with the given identifier.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(GlobalRateLimit), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        var limit = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Global rate limit '{id}' not found");
        return Ok(limit);
    }

    /// <summary>
    /// Creates a new global rate limit.
    /// </summary>
    /// <param name="limit">The global rate limit to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created global rate limit.</returns>
    /// <response code="201">The global rate limit was created successfully.</response>
    /// <response code="409">A global rate limit for the same target already exists.</response>
    [HttpPost]
    [ProducesResponseType(typeof(GlobalRateLimit), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] GlobalRateLimit limit, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByTargetAsync(limit.TargetId, limit.TargetType, cancellationToken);
        if (existing is not null)
        {
            throw new ConflictException($"A global rate limit already exists for {limit.TargetType} '{limit.TargetId}'");
        }

        await _repository.CreateAsync(limit, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = limit.Id }, limit);
    }

    /// <summary>
    /// Updates an existing global rate limit.
    /// </summary>
    /// <param name="id">The unique identifier of the global rate limit to update.</param>
    /// <param name="limit">The updated global rate limit.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated global rate limit.</returns>
    /// <response code="200">The global rate limit was updated successfully.</response>
    /// <response code="404">No global rate limit was found with the given identifier.</response>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(GlobalRateLimit), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string id, [FromBody] GlobalRateLimit limit, CancellationToken cancellationToken)
    {
        var updated = limit with { Id = id };
        await _repository.UpdateAsync(updated, cancellationToken);
        return Ok(updated);
    }

    /// <summary>
    /// Deletes a global rate limit.
    /// </summary>
    /// <param name="id">The unique identifier of the global rate limit to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="204">The global rate limit was deleted successfully.</response>
    /// <response code="404">No global rate limit was found with the given identifier.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        await _repository.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
