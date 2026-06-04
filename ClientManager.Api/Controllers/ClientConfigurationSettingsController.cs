using Asp.Versioning;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;
using ClientManager.Shared.Models.Problems;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.Api.Controllers;

/// <summary>
/// Manages per-client service access, resource pool, and global rate-limit settings.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/clients")]
[Tags("Client Configurations")]
public class ClientConfigurationSettingsController(
    IClientServiceSettingsService clientServiceSettingsService,
    IClientResourcePoolSettingsService clientResourcePoolSettingsService,
    IClientGlobalRateLimitService clientGlobalRateLimitService) : ControllerBase
{
    /// <summary>
    /// Lists all service access settings for a client, paginated.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="paging">Pagination parameters.</param>
    /// <param name="cancellationToken">Token used to cancel the service settings listing before it completes.</param>
    /// <returns>A paginated list of service access setting entries.</returns>
    /// <response code="200">Returns the paginated service access settings.</response>
    /// <response code="404">No client was found with the given identifier.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpGet("{id}/services")]
    [ProducesResponseType(typeof(PagedResponse<KeyedEntry<ServiceAccessSettings>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetServices(string id, [FromQuery] PagedRequest paging, CancellationToken cancellationToken)
    {
        var settings = await clientServiceSettingsService.GetServicesAsync(id, paging, cancellationToken);
        return Ok(settings);
    }

    /// <summary>
    /// Gets service access settings for a specific service.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="serviceId">The unique identifier of the service.</param>
    /// <param name="cancellationToken">Token used to cancel the service settings lookup before it completes.</param>
    /// <returns>The service access settings.</returns>
    /// <response code="200">Returns the service access settings.</response>
    /// <response code="404">Client or service settings not found.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpGet("{id}/services/{serviceId}")]
    [ProducesResponseType(typeof(ServiceAccessSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetServiceSettings(string id, string serviceId, CancellationToken cancellationToken)
    {
        var settings = await clientServiceSettingsService.GetServiceSettingsAsync(id, serviceId, cancellationToken);
        return Ok(settings);
    }

    /// <summary>
    /// Creates or updates service access settings for a specific service.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="serviceId">The unique identifier of the service.</param>
    /// <param name="settings">The service access settings to apply.</param>
    /// <param name="cancellationToken">Token used to abort the service settings update before it is persisted.</param>
    /// <response code="200">The service access settings were updated.</response>
    /// <response code="404">No client was found with the given identifier.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpPut("{id}/services/{serviceId}")]
    [ProducesResponseType(typeof(ServiceAccessSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SetServiceSettings(
        string id,
        string serviceId,
        [FromBody] ServiceAccessSettings settings,
        CancellationToken cancellationToken)
    {
        var applied = await clientServiceSettingsService.SetServiceSettingsAsync(id, serviceId, settings, cancellationToken);
        return Ok(applied);
    }

    /// <summary>
    /// Removes service access settings for a specific service (revokes access).
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="serviceId">The unique identifier of the service.</param>
    /// <param name="cancellationToken">Token used to abort the service settings removal before it completes.</param>
    /// <response code="204">The service access settings were removed.</response>
    /// <response code="404">No client was found with the given identifier.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpDelete("{id}/services/{serviceId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> RemoveServiceSettings(string id, string serviceId, CancellationToken cancellationToken)
    {
        await clientServiceSettingsService.RemoveServiceSettingsAsync(id, serviceId, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Lists all resource pool settings for a client, paginated.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="paging">Pagination parameters.</param>
    /// <param name="cancellationToken">Token used to cancel the resource pool settings listing before it completes.</param>
    /// <returns>A paginated list of resource pool setting entries.</returns>
    /// <response code="200">Returns the paginated resource pool settings.</response>
    /// <response code="404">No client was found with the given identifier.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpGet("{id}/resource-pools")]
    [ProducesResponseType(typeof(PagedResponse<KeyedEntry<ResourcePoolSettings>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetResourcePools(string id, [FromQuery] PagedRequest paging, CancellationToken cancellationToken)
    {
        var settings = await clientResourcePoolSettingsService.GetResourcePoolsAsync(id, paging, cancellationToken);
        return Ok(settings);
    }

    /// <summary>
    /// Gets resource pool settings for a specific pool.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="poolId">The unique identifier of the resource pool.</param>
    /// <param name="cancellationToken">Token used to cancel the resource pool settings lookup before it completes.</param>
    /// <returns>The resource pool settings.</returns>
    /// <response code="200">Returns the resource pool settings.</response>
    /// <response code="404">Client or pool settings not found.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpGet("{id}/resource-pools/{poolId}")]
    [ProducesResponseType(typeof(ResourcePoolSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetResourcePoolSettings(string id, string poolId, CancellationToken cancellationToken)
    {
        var settings = await clientResourcePoolSettingsService.GetResourcePoolSettingsAsync(id, poolId, cancellationToken);
        return Ok(settings);
    }

    /// <summary>
    /// Creates or updates resource pool settings for a specific pool.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="poolId">The unique identifier of the resource pool.</param>
    /// <param name="settings">The resource pool settings to apply.</param>
    /// <param name="cancellationToken">Token used to abort the resource pool settings update before it is persisted.</param>
    /// <response code="200">The resource pool settings were updated.</response>
    /// <response code="404">No client was found with the given identifier.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpPut("{id}/resource-pools/{poolId}")]
    [ProducesResponseType(typeof(ResourcePoolSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SetResourcePoolSettings(
        string id,
        string poolId,
        [FromBody] ResourcePoolSettings settings,
        CancellationToken cancellationToken)
    {
        var applied = await clientResourcePoolSettingsService.SetResourcePoolSettingsAsync(id, poolId, settings, cancellationToken);
        return Ok(applied);
    }

    /// <summary>
    /// Removes resource pool settings for a specific pool.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="poolId">The unique identifier of the resource pool.</param>
    /// <param name="cancellationToken">Token used to abort the resource pool settings removal before it completes.</param>
    /// <response code="204">The resource pool settings were removed.</response>
    /// <response code="404">No client was found with the given identifier.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpDelete("{id}/resource-pools/{poolId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> RemoveResourcePoolSettings(string id, string poolId, CancellationToken cancellationToken)
    {
        await clientResourcePoolSettingsService.RemoveResourcePoolSettingsAsync(id, poolId, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Gets the client's global rate limit.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="cancellationToken">Token used to cancel the client global rate limit lookup before it completes.</param>
    /// <returns>The client's global rate limit.</returns>
    /// <response code="200">Returns the global rate limit.</response>
    /// <response code="404">Client not found or no global rate limit is configured.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpGet("{id}/global-rate-limit")]
    [ProducesResponseType(typeof(ClientRateLimit), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetGlobalRateLimit(string id, CancellationToken cancellationToken)
    {
        var rateLimit = await clientGlobalRateLimitService.GetGlobalRateLimitAsync(id, cancellationToken);
        return Ok(rateLimit);
    }

    /// <summary>
    /// Sets the client's global rate limit.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="rateLimit">The global rate limit to apply.</param>
    /// <param name="cancellationToken">Token used to abort the client global rate limit update before it is persisted.</param>
    /// <response code="200">The global rate limit was set.</response>
    /// <response code="404">No client was found with the given identifier.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpPut("{id}/global-rate-limit")]
    [ProducesResponseType(typeof(ClientRateLimit), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SetGlobalRateLimit(string id, [FromBody] ClientRateLimit rateLimit, CancellationToken cancellationToken)
    {
        var applied = await clientGlobalRateLimitService.SetGlobalRateLimitAsync(id, rateLimit, cancellationToken);
        return Ok(applied);
    }

    /// <summary>
    /// Removes the client's global rate limit.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="cancellationToken">Token used to abort the client global rate limit removal before it completes.</param>
    /// <response code="204">The global rate limit was removed.</response>
    /// <response code="404">No client was found with the given identifier.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpDelete("{id}/global-rate-limit")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> RemoveGlobalRateLimit(string id, CancellationToken cancellationToken)
    {
        await clientGlobalRateLimitService.RemoveGlobalRateLimitAsync(id, cancellationToken);
        return NoContent();
    }
}
