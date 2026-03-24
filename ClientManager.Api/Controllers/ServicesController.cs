using Asp.Versioning;
using ClientManager.Api.Extensions;
using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Models.Requests;
using ClientManager.Api.Models.Responses;
using ClientManager.DataAccess.Repositories.Interfaces;
using ClientManager.Shared.Models.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.Api.Controllers;

/// <summary>
/// Manages system-wide service definitions.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/services")]
[Tags("Services")]
public class ServicesController : ControllerBase
{
    private readonly IEntityRepository<Service> _repository;

    /// <summary>
    /// Initializes a new instance of <see cref="ServicesController"/>.
    /// </summary>
    /// <param name="repository">The service entity repository.</param>
    public ServicesController(IEntityRepository<Service> repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Lists all services with optional filtering and pagination.
    /// </summary>
    /// <param name="paging">Pagination parameters.</param>
    /// <param name="isEnabled">Optional filter by enabled state.</param>
    /// <param name="name">Optional case-insensitive name filter (contains match).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated list of services.</returns>
    /// <response code="200">Returns the paginated services.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<Service>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] PagedRequest paging,
        [FromQuery] bool? isEnabled,
        [FromQuery] string? name,
        CancellationToken cancellationToken)
    {
        var services = await _repository.GetAllAsync(cancellationToken);

        IReadOnlyList<Service> filtered = services
            .Where(s => !isEnabled.HasValue || s.IsEnabled == isEnabled.Value)
            .Where(s => name is null || s.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return Ok(filtered.ToPagedResponse(paging));
    }

    /// <summary>
    /// Retrieves a service by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the service.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The service.</returns>
    /// <response code="200">Returns the requested service.</response>
    /// <response code="404">No service was found with the given identifier.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Service), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        var service = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Service '{id}' not found");
        return Ok(service);
    }

    /// <summary>
    /// Creates a new service.
    /// </summary>
    /// <param name="service">The service to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created service.</returns>
    /// <response code="201">The service was created successfully.</response>
    /// <response code="409">A service with the same identifier already exists.</response>
    [HttpPost]
    [ProducesResponseType(typeof(Service), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] Service service, CancellationToken cancellationToken)
    {
        await _repository.CreateAsync(service, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = service.Id }, service);
    }

    /// <summary>
    /// Updates an existing service.
    /// </summary>
    /// <param name="id">The unique identifier of the service to update.</param>
    /// <param name="service">The updated service.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated service.</returns>
    /// <response code="200">The service was updated successfully.</response>
    /// <response code="404">No service was found with the given identifier.</response>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(Service), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string id, [FromBody] Service service, CancellationToken cancellationToken)
    {
        var updated = service with { Id = id };
        await _repository.UpdateAsync(updated, cancellationToken);
        return Ok(updated);
    }

    /// <summary>
    /// Deletes a service.
    /// </summary>
    /// <param name="id">The unique identifier of the service to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="204">The service was deleted successfully.</response>
    /// <response code="404">No service was found with the given identifier.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        await _repository.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
