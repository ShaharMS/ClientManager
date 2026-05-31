using Asp.Versioning;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.Api.Controllers;

/// <summary>
/// Manages client configuration documents and their top-level CRUD operations.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/clients")]
[Tags("Client Configurations")]
public class ClientConfigurationsController : ControllerBase
{
    private readonly IClientConfigurationService _clientConfigurationService;

    /// <summary>
    /// Initializes a new instance of <see cref="ClientConfigurationsController"/>.
    /// </summary>
    /// <param name="clientConfigurationService">The client configuration service.</param>
    public ClientConfigurationsController(IClientConfigurationService clientConfigurationService)
    {
        _clientConfigurationService = clientConfigurationService;
    }

    /// <summary>
    /// Searches client configurations with optional filtering, sorting, and pagination.
    /// </summary>
    /// <param name="query">Query with filters, sort, and pagination. Pass an empty body or null for all results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching client configurations and total count.</returns>
    /// <response code="200">Returns the matching client configurations.</response>
    [HttpPost("search")]
    [ProducesResponseType(typeof(SearchResult<ClientConfiguration>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromBody] DocumentQuery? query, CancellationToken cancellationToken)
    {
        var configurations = await _clientConfigurationService.SearchAsync(query ?? DocumentQuery.All, cancellationToken);
        return Ok(configurations);
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
        var configuration = await _clientConfigurationService.GetByIdAsync(id, cancellationToken);
        return Ok(configuration);
    }

    /// <summary>
    /// Creates a new client configuration.
    /// </summary>
    /// <param name="configuration">The client configuration to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created client configuration.</returns>
    /// <response code="201">The client configuration was created successfully.</response>
    /// <response code="409">A client configuration with the same identifier already exists.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ClientConfiguration), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] ClientConfiguration configuration, CancellationToken cancellationToken)
    {
        var created = await _clientConfigurationService.CreateAsync(configuration, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
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
        var updated = await _clientConfigurationService.UpdateAsync(id, configuration, cancellationToken);
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
        await _clientConfigurationService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}