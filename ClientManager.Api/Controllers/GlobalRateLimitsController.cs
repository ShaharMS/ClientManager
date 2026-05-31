using Asp.Versioning;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Shared.Models.Search;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Problems;
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
    private readonly IGlobalRateLimitCatalogService _globalRateLimitCatalogService;

    /// <summary>
    /// Initializes a new instance of <see cref="GlobalRateLimitsController"/>.
    /// </summary>
    /// <param name="globalRateLimitCatalogService">The global rate limit catalog service.</param>
    public GlobalRateLimitsController(IGlobalRateLimitCatalogService globalRateLimitCatalogService)
    {
        _globalRateLimitCatalogService = globalRateLimitCatalogService;
    }

    /// <summary>
    /// Searches global rate limits with optional filtering, sorting, and pagination.
    /// </summary>
    /// <param name="query">Query with filters, sort, and pagination. Pass an empty body or null for all results.</param>
    /// <param name="cancellationToken">Token used to cancel the global rate limit search before it completes.</param>
    /// <returns>Matching global rate limits and total count.</returns>
    /// <response code="200">Returns the matching global rate limits.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpPost("search")]
    [ProducesResponseType(typeof(SearchResult<GlobalRateLimit>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Search(
        [FromBody] DocumentQuery? query,
        CancellationToken cancellationToken)
    {
        var limits = await _globalRateLimitCatalogService.SearchAsync(query ?? DocumentQuery.All, cancellationToken);
        return Ok(limits);
    }

    /// <summary>
    /// Retrieves a global rate limit by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the global rate limit.</param>
    /// <param name="cancellationToken">Token used to cancel the global rate limit lookup before it completes.</param>
    /// <returns>The global rate limit.</returns>
    /// <response code="200">Returns the requested global rate limit.</response>
    /// <response code="404">No global rate limit was found with the given identifier.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(GlobalRateLimit), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        var limit = await _globalRateLimitCatalogService.GetByIdAsync(id, cancellationToken);
        return Ok(limit);
    }

    /// <summary>
    /// Creates a new global rate limit.
    /// </summary>
    /// <param name="limit">The global rate limit to create.</param>
    /// <param name="cancellationToken">Token used to abort the create-global-rate-limit request before it is persisted.</param>
    /// <returns>The created global rate limit.</returns>
    /// <response code="201">The global rate limit was created successfully.</response>
    /// <response code="409">A global rate limit for the same target already exists.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpPost]
    [ProducesResponseType(typeof(GlobalRateLimit), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Create([FromBody] GlobalRateLimit limit, CancellationToken cancellationToken)
    {
        var created = await _globalRateLimitCatalogService.CreateAsync(limit, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Updates an existing global rate limit.
    /// </summary>
    /// <param name="id">The unique identifier of the global rate limit to update.</param>
    /// <param name="limit">The updated global rate limit.</param>
    /// <param name="cancellationToken">Token used to abort the global rate limit update before it is persisted.</param>
    /// <returns>The updated global rate limit.</returns>
    /// <response code="200">The global rate limit was updated successfully.</response>
    /// <response code="404">No global rate limit was found with the given identifier.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(GlobalRateLimit), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Update(string id, [FromBody] GlobalRateLimit limit, CancellationToken cancellationToken)
    {
        var updated = await _globalRateLimitCatalogService.UpdateAsync(id, limit, cancellationToken);
        return Ok(updated);
    }

    /// <summary>
    /// Deletes a global rate limit.
    /// </summary>
    /// <param name="id">The unique identifier of the global rate limit to delete.</param>
    /// <param name="cancellationToken">Token used to abort the global rate limit deletion before it completes.</param>
    /// <response code="204">The global rate limit was deleted successfully.</response>
    /// <response code="404">No global rate limit was found with the given identifier.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        await _globalRateLimitCatalogService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
