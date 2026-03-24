using Asp.Versioning;
using ClientManager.Api.Interfaces;
using ClientManager.Api.Models.Requests;
using ClientManager.Api.Models.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.Api.Controllers;

/// <summary>
/// Operational endpoints for acquiring and releasing resource pool slots.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/resources")]
[Tags("Resource Allocation")]
public class ResourceAllocationController : ControllerBase
{
    private readonly IResourceAllocationService _allocationService;

    /// <summary>
    /// Initializes a new instance of <see cref="ResourceAllocationController"/>.
    /// </summary>
    /// <param name="allocationService">The resource allocation service.</param>
    public ResourceAllocationController(IResourceAllocationService allocationService)
    {
        _allocationService = allocationService;
    }

    /// <summary>
    /// Acquires a resource slot from a resource pool.
    /// </summary>
    /// <param name="request">The acquire request containing client and resource pool IDs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The allocation response with allocation ID and expiry time.</returns>
    /// <response code="200">The resource slot was acquired successfully.</response>
    /// <response code="403">Client is disabled.</response>
    /// <response code="404">Client or resource pool not found.</response>
    /// <response code="429">Slot limit or rate limit exceeded.</response>
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
    /// Releases a previously acquired resource slot.
    /// </summary>
    /// <param name="request">The release request containing the allocation ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Whether the allocation was released.</returns>
    /// <response code="200">The allocation was released or was already released.</response>
    /// <response code="404">No allocation was found with the given identifier.</response>
    [HttpPost("release")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Release([FromBody] ReleaseResourceRequest request, CancellationToken cancellationToken)
    {
        var released = await _allocationService.ReleaseAsync(request.AllocationId, cancellationToken);
        return Ok(new { Released = released });
    }
}
