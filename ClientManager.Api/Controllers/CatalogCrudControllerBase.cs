using ClientManager.Api.Services.Interfaces;
using ClientManager.Shared.Models.Problems;
using ClientManager.Shared.Models.Search;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.Api.Controllers;

/// <summary>
/// Shared Search/GetById/Create/Update/Delete actions for catalog controllers.
/// </summary>
/// <remarks>
/// <para>
/// Catalog entities (clients, services, global rate limits) share the same management shape: searchable
/// lists, fetch-by-id for editors, and full-document create/update/delete. This base keeps those routes
/// and XML docs consistent so Swagger and the Admin UI see one predictable contract.
/// </para>
/// <para>
/// Updates are full PUT replacements. Partial PATCH was removed to avoid competing edit paths now that
/// the Admin UI always saves complete documents.
/// </para>
/// </remarks>
/// <typeparam name="TEntity">The catalog entity type.</typeparam>
public abstract class CatalogCrudControllerBase<TEntity>(ICatalogCrudService<TEntity> catalog) : ControllerBase
    where TEntity : class
{
    /// <summary>
    /// Searches catalog entries with optional filtering, sorting, and pagination.
    /// </summary>
    /// <param name="query">Query with filters, sort, and pagination. Pass an empty body or null for all results.</param>
    /// <param name="cancellationToken">Token used to cancel the search before it completes.</param>
    /// <returns>Matching entries and total count.</returns>
    /// <response code="200">Returns the matching entries.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpPost("search")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Search(
        [FromBody] DocumentQuery? query,
        CancellationToken cancellationToken)
    {
        var results = await catalog.SearchAsync(query ?? DocumentQuery.All, cancellationToken);
        return Ok(results);
    }

    /// <summary>
    /// Retrieves a catalog entry by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the entry.</param>
    /// <param name="cancellationToken">Token used to cancel the lookup before it completes.</param>
    /// <returns>The catalog entry.</returns>
    /// <response code="200">Returns the requested entry.</response>
    /// <response code="404">No entry was found with the given identifier.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        var entity = await catalog.GetByIdAsync(id, cancellationToken);
        return Ok(entity);
    }

    /// <summary>
    /// Creates a new catalog entry.
    /// </summary>
    /// <param name="entity">The entry to create.</param>
    /// <param name="cancellationToken">Token used to abort the create request before it is persisted.</param>
    /// <returns>The created entry.</returns>
    /// <response code="201">The entry was created successfully.</response>
    /// <response code="409">An entry with the same identifier already exists.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Create([FromBody] TEntity entity, CancellationToken cancellationToken)
    {
        var created = await catalog.CreateAsync(entity, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = GetEntityId(created) }, created);
    }

    /// <summary>
    /// Updates an existing catalog entry (full document replace).
    /// </summary>
    /// <param name="id">The unique identifier of the entry to update.</param>
    /// <param name="entity">The updated entry.</param>
    /// <param name="cancellationToken">Token used to abort the update before it is persisted.</param>
    /// <returns>The updated entry.</returns>
    /// <response code="200">The entry was updated successfully.</response>
    /// <response code="404">No entry was found with the given identifier.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Update(string id, [FromBody] TEntity entity, CancellationToken cancellationToken)
    {
        var updated = await catalog.UpdateAsync(id, entity, cancellationToken);
        return Ok(updated);
    }

    /// <summary>
    /// Deletes a catalog entry.
    /// </summary>
    /// <param name="id">The unique identifier of the entry to delete.</param>
    /// <param name="cancellationToken">Token used to abort the deletion before it completes.</param>
    /// <response code="204">The entry was deleted successfully.</response>
    /// <response code="404">No entry was found with the given identifier.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        await catalog.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    private static string GetEntityId(TEntity entity)
    {
        var idProperty = typeof(TEntity).GetProperty("Id")
            ?? throw new InvalidOperationException($"Catalog entity {typeof(TEntity).Name} must expose an Id property.");

        return (string?)idProperty.GetValue(entity)
            ?? throw new InvalidOperationException($"Catalog entity {typeof(TEntity).Name} Id must not be null.");
    }
}
