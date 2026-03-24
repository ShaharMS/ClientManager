using Asp.Versioning;
using ClientManager.Api.Models.Exceptions;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.Shared.Models.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.Api.Controllers;

/// <summary>
/// Manages client configuration documents and their nested sub-resources.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/clients")]
[Tags("Client Configurations")]
public class ClientConfigurationsController : ControllerBase
{
    private readonly IClientConfigurationRepository _repository;

    /// <summary>
    /// Initializes a new instance of <see cref="ClientConfigurationsController"/>.
    /// </summary>
    /// <param name="repository">The client configuration repository.</param>
    public ClientConfigurationsController(IClientConfigurationRepository repository)
    {
        _repository = repository;
    }

    #region Top-level client config CRUD

    /// <summary>
    /// Lists all client configurations.
    /// </summary>
    /// <returns>A list of all client configurations.</returns>
    /// <response code="200">Returns all client configurations.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ClientConfiguration>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var configs = await _repository.GetAllAsync(cancellationToken);
        return Ok(configs);
    }

    /// <summary>
    /// Retrieves a client configuration by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The client configuration.</returns>
    /// <response code="200">Returns the requested client configuration.</response>
    /// <response code="404">No client was found with the given identifier.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ClientConfiguration), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        var config = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Client '{id}' not found");
        return Ok(config);
    }

    /// <summary>
    /// Creates a new client configuration.
    /// </summary>
    /// <param name="configuration">The client configuration to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created client configuration.</returns>
    /// <response code="201">The client configuration was created successfully.</response>
    /// <response code="409">A client with the same identifier already exists.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ClientConfiguration), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] ClientConfiguration configuration, CancellationToken cancellationToken)
    {
        await _repository.CreateAsync(configuration, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = configuration.Id }, configuration);
    }

    /// <summary>
    /// Updates an existing client configuration (full document replace).
    /// </summary>
    /// <param name="id">The unique identifier of the client to update.</param>
    /// <param name="configuration">The updated client configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated client configuration.</returns>
    /// <response code="200">The client configuration was updated successfully.</response>
    /// <response code="404">No client was found with the given identifier.</response>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ClientConfiguration), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string id, [FromBody] ClientConfiguration configuration, CancellationToken cancellationToken)
    {
        var updated = configuration with { Id = id };
        await _repository.UpdateAsync(updated, cancellationToken);
        return Ok(updated);
    }

    /// <summary>
    /// Deletes a client configuration.
    /// </summary>
    /// <param name="id">The unique identifier of the client to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="204">The client configuration was deleted successfully.</response>
    /// <response code="404">No client was found with the given identifier.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        await _repository.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    #endregion

    #region Sub-resource: Per-service access settings

    /// <summary>
    /// Lists all service access settings for a client.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The service access settings dictionary.</returns>
    /// <response code="200">Returns the service access settings.</response>
    /// <response code="404">No client was found with the given identifier.</response>
    [HttpGet("{id}/services")]
    [ProducesResponseType(typeof(Dictionary<string, ServiceAccessSettings>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetServices(string id, CancellationToken cancellationToken)
    {
        var config = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Client '{id}' not found");
        return Ok(config.Services);
    }

    /// <summary>
    /// Gets service access settings for a specific service.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="serviceId">The unique identifier of the service.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The service access settings.</returns>
    /// <response code="200">Returns the service access settings.</response>
    /// <response code="404">Client or service settings not found.</response>
    [HttpGet("{id}/services/{serviceId}")]
    [ProducesResponseType(typeof(ServiceAccessSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetServiceSettings(string id, string serviceId, CancellationToken cancellationToken)
    {
        var settings = await _repository.GetServiceSettingsAsync(id, serviceId, cancellationToken)
            ?? throw new NotFoundException($"Service settings for '{serviceId}' not found on client '{id}'");
        return Ok(settings);
    }

    /// <summary>
    /// Creates or updates service access settings for a specific service.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="serviceId">The unique identifier of the service.</param>
    /// <param name="settings">The service access settings to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The service access settings were updated.</response>
    /// <response code="404">No client was found with the given identifier.</response>
    [HttpPut("{id}/services/{serviceId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetServiceSettings(string id, string serviceId, [FromBody] ServiceAccessSettings settings, CancellationToken cancellationToken)
    {
        await _repository.SetServiceSettingsAsync(id, serviceId, settings, cancellationToken);
        return Ok(settings);
    }

    /// <summary>
    /// Removes service access settings for a specific service (revokes access).
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="serviceId">The unique identifier of the service.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="204">The service access settings were removed.</response>
    /// <response code="404">No client was found with the given identifier.</response>
    [HttpDelete("{id}/services/{serviceId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveServiceSettings(string id, string serviceId, CancellationToken cancellationToken)
    {
        await _repository.RemoveServiceSettingsAsync(id, serviceId, cancellationToken);
        return NoContent();
    }

    #endregion

    #region Sub-resource: Per-pool resource settings

    /// <summary>
    /// Lists all resource pool settings for a client.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resource pool settings dictionary.</returns>
    /// <response code="200">Returns the resource pool settings.</response>
    /// <response code="404">No client was found with the given identifier.</response>
    [HttpGet("{id}/resource-pools")]
    [ProducesResponseType(typeof(Dictionary<string, ResourcePoolSettings>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResourcePools(string id, CancellationToken cancellationToken)
    {
        var config = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Client '{id}' not found");
        return Ok(config.ResourcePools);
    }

    /// <summary>
    /// Gets resource pool settings for a specific pool.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="poolId">The unique identifier of the resource pool.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resource pool settings.</returns>
    /// <response code="200">Returns the resource pool settings.</response>
    /// <response code="404">Client or pool settings not found.</response>
    [HttpGet("{id}/resource-pools/{poolId}")]
    [ProducesResponseType(typeof(ResourcePoolSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResourcePoolSettings(string id, string poolId, CancellationToken cancellationToken)
    {
        var settings = await _repository.GetResourcePoolSettingsAsync(id, poolId, cancellationToken)
            ?? throw new NotFoundException($"Resource pool settings for '{poolId}' not found on client '{id}'");
        return Ok(settings);
    }

    /// <summary>
    /// Creates or updates resource pool settings for a specific pool.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="poolId">The unique identifier of the resource pool.</param>
    /// <param name="settings">The resource pool settings to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The resource pool settings were updated.</response>
    /// <response code="404">No client was found with the given identifier.</response>
    [HttpPut("{id}/resource-pools/{poolId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetResourcePoolSettings(string id, string poolId, [FromBody] ResourcePoolSettings settings, CancellationToken cancellationToken)
    {
        await _repository.SetResourcePoolSettingsAsync(id, poolId, settings, cancellationToken);
        return Ok(settings);
    }

    /// <summary>
    /// Removes resource pool settings for a specific pool.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="poolId">The unique identifier of the resource pool.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="204">The resource pool settings were removed.</response>
    /// <response code="404">No client was found with the given identifier.</response>
    [HttpDelete("{id}/resource-pools/{poolId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveResourcePoolSettings(string id, string poolId, CancellationToken cancellationToken)
    {
        await _repository.RemoveResourcePoolSettingsAsync(id, poolId, cancellationToken);
        return NoContent();
    }

    #endregion

    #region Sub-resource: Client global rate limit

    /// <summary>
    /// Gets the client's global rate limit.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The client's global rate limit.</returns>
    /// <response code="200">Returns the global rate limit.</response>
    /// <response code="404">Client not found or no global rate limit is configured.</response>
    [HttpGet("{id}/global-rate-limit")]
    [ProducesResponseType(typeof(ClientRateLimit), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGlobalRateLimit(string id, CancellationToken cancellationToken)
    {
        var config = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Client '{id}' not found");
        return config.GlobalRateLimit is not null
            ? Ok(config.GlobalRateLimit)
            : throw new NotFoundException($"No global rate limit configured for client '{id}'");
    }

    /// <summary>
    /// Sets the client's global rate limit.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="rateLimit">The global rate limit to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The global rate limit was set.</response>
    /// <response code="404">No client was found with the given identifier.</response>
    [HttpPut("{id}/global-rate-limit")]
    [ProducesResponseType(typeof(ClientRateLimit), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetGlobalRateLimit(string id, [FromBody] ClientRateLimit rateLimit, CancellationToken cancellationToken)
    {
        var config = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Client '{id}' not found");
        var updated = config with { GlobalRateLimit = rateLimit };
        await _repository.UpdateAsync(updated, cancellationToken);
        return Ok(rateLimit);
    }

    /// <summary>
    /// Removes the client's global rate limit.
    /// </summary>
    /// <param name="id">The unique identifier of the client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="204">The global rate limit was removed.</response>
    /// <response code="404">No client was found with the given identifier.</response>
    [HttpDelete("{id}/global-rate-limit")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveGlobalRateLimit(string id, CancellationToken cancellationToken)
    {
        var config = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Client '{id}' not found");
        var updated = config with { GlobalRateLimit = null };
        await _repository.UpdateAsync(updated, cancellationToken);
        return NoContent();
    }

    #endregion
}
