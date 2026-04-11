using Asp.Versioning;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;
using ClientManager.StorageApi.Models.Exceptions;
using ClientManager.StorageApi.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.StorageApi.Controllers;

/// <summary>
/// Manages internal global-rate-limit catalog endpoints.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("internal/v{version:apiVersion}/configuration/global-rate-limits")]
[Tags("Global Rate Limits")]
public class GlobalRateLimitsController : ControllerBase
{
    private readonly IGlobalRateLimitCatalogService _service;

    public GlobalRateLimitsController(IGlobalRateLimitCatalogService service)
    {
        _service = service;
    }

    /// <summary>
    /// Searches global rate limits with optional filtering, sorting, and pagination.
    /// </summary>
    /// <param name="query">Query with filters, sort, and pagination. Pass an empty body or null for all results.</param>
    /// <response code="200">Returns the matching global rate limits.</response>
    [HttpPost("search")]
    [ProducesResponseType(typeof(SearchResult<GlobalRateLimit>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromBody] DocumentQuery? query, CancellationToken cancellationToken)
    {
        var result = await _service.SearchAsync(query ?? DocumentQuery.All, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a global rate limit by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the global rate limit.</param>
    /// <response code="200">Returns the requested global rate limit.</response>
    /// <response code="404">No global rate limit was found with the given identifier.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(GlobalRateLimit), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        var limit = await _service.GetByIdAsync(id, cancellationToken)
            ?? throw new GlobalRateLimitNotFoundException(id);
        return Ok(limit);
    }

    /// <summary>
    /// Creates a new global rate limit.
    /// </summary>
    /// <param name="limit">The global rate limit to create.</param>
    /// <response code="201">The global rate limit was created successfully.</response>
    /// <response code="409">A global rate limit for the same target already exists.</response>
    [HttpPost]
    [ProducesResponseType(typeof(GlobalRateLimit), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] GlobalRateLimit limit, CancellationToken cancellationToken)
    {
        await _service.CreateAsync(limit, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { version = "1.0", id = limit.Id }, limit);
    }

    /// <summary>
    /// Updates an existing global rate limit.
    /// </summary>
    /// <param name="id">The unique identifier of the global rate limit to update.</param>
    /// <param name="limit">The updated global rate limit.</param>
    /// <response code="200">The global rate limit was updated successfully.</response>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(GlobalRateLimit), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(string id, [FromBody] GlobalRateLimit limit, CancellationToken cancellationToken)
    {
        var updated = await _service.UpdateAsync(id, limit, cancellationToken);
        return Ok(updated);
    }

    /// <summary>
    /// Deletes a global rate limit.
    /// </summary>
    /// <param name="id">The unique identifier of the global rate limit to delete.</param>
    /// <response code="204">The global rate limit was deleted successfully.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}