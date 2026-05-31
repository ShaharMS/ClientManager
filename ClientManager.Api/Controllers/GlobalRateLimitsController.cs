using Asp.Versioning;
using ClientManager.Api.Services.InternalClients.Interfaces.Configuration;
using ClientManager.Shared.Models.Search;
using ClientManager.Shared.Models.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.Api.Controllers;

/// <summary>
/// Manages system-wide catch-all rate limits for services and resource pools.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/global-rate-limits")]
[Tags("Global Rate Limits")]
public class GlobalRateLimitsController : ControllerBase
{
    private readonly IGlobalRateLimitCatalogClient _globalRateLimitCatalogClient;

    /// <summary>
    /// Initializes a new instance of <see cref="GlobalRateLimitsController"/>.
    /// </summary>
    /// <param name="globalRateLimitCatalogClient">The internal global rate limit catalog client.</param>
    public GlobalRateLimitsController(IGlobalRateLimitCatalogClient globalRateLimitCatalogClient)
    {
        _globalRateLimitCatalogClient = globalRateLimitCatalogClient;
    }

    /// <summary>
    /// Searches global rate limits with optional filtering, sorting, and pagination.
    /// </summary>
    /// <param name="query">Query with filters, sort, and pagination. Pass an empty body or null for all results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching global rate limits and total count.</returns>
    /// <response code="200">Returns the matching global rate limits.</response>
    [HttpPost("search")]
    [ProducesResponseType(typeof(SearchResult<GlobalRateLimit>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromBody] DocumentQuery? query,
        CancellationToken cancellationToken)
    {
        var result = await _globalRateLimitCatalogClient.SearchAsync(query ?? DocumentQuery.All, cancellationToken);
        return Ok(result);
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
        var limit = await _globalRateLimitCatalogClient.GetByIdAsync(id, cancellationToken);
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
        await _globalRateLimitCatalogClient.CreateAsync(limit, cancellationToken);
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
        await _globalRateLimitCatalogClient.UpdateAsync(id, limit, cancellationToken);
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
        await _globalRateLimitCatalogClient.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
