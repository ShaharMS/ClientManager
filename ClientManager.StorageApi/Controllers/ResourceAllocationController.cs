using Asp.Versioning;
using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;
using ClientManager.StorageApi.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.StorageApi.Controllers;

/// <summary>
/// Internal runtime endpoints for resource acquisition and release.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("internal/v{version:apiVersion}/runtime/resources")]
[Tags("Runtime Operations")]
public class ResourceAllocationController : ControllerBase
{
    private readonly IResourceAllocationService _allocationService;

    /// <summary>
    /// Initializes a new instance of <see cref="ResourceAllocationController"/>.
    /// </summary>
    /// <param name="allocationService">The runtime allocation service.</param>
    public ResourceAllocationController(IResourceAllocationService allocationService)
    {
        _allocationService = allocationService;
    }

    /// <summary>
    /// Acquires a resource slot from a pool.
    /// </summary>
    /// <param name="request">The resource-acquire request.</param>
    /// <param name="cancellationToken">Cancels the allocation pipeline.</param>
    /// <response code="200">Returns the allocation identifier and expiry time.</response>
    /// <response code="403">The client is disabled.</response>
    /// <response code="404">The client or resource pool does not exist.</response>
    /// <response code="429">The client cap, pool capacity, or global pool rate limit was exceeded.</response>
    [HttpPost("acquire")]
    [ProducesResponseType(typeof(ResourceAcquireResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Acquire([FromBody] AcquireResourceRequest request, CancellationToken cancellationToken)
    {
        var response = await _allocationService.AcquireAsync(request.ClientId, request.ResourcePoolId, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Releases a previously acquired resource allocation.
    /// </summary>
    /// <param name="request">The resource-release request.</param>
    /// <param name="cancellationToken">Cancels the release operation.</param>
    /// <response code="200">Returns whether the allocation was newly released.</response>
    /// <response code="404">No allocation was found with the given identifier.</response>
    [HttpPost("release")]
    [ProducesResponseType(typeof(ResourceReleaseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Release([FromBody] ReleaseResourceRequest request, CancellationToken cancellationToken)
    {
        var response = await _allocationService.ReleaseAsync(request.AllocationId, cancellationToken);
        return Ok(response);
    }
}