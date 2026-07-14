using ClientManager.Api.Filters;
using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Services.Storage;
using ClientManager.Api.Utils;
using ClientManager.Shared.Configuration.Storage;
using ClientManager.Shared.Models.Problems;
using ClientManager.Shared.Models.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.Api.Controllers;

/// <summary>
/// Exports and imports catalog seed data for copying configuration between instances.
/// </summary>
/// <remarks>
/// <para><strong>Response format</strong></para>
/// <para>
/// Export returns JSON in the <see cref="SeedOptions"/> shape suitable for backup files or
/// appsettings-style bundles.
/// </para>
/// <para><strong>Long-running operations</strong></para>
/// <para>
/// Export, import, and delete can take noticeable time on large catalogs. Only one seed operation may
/// run at a time; concurrent requests receive HTTP 409. Do not start another seed call until the current
/// one finishes.
/// </para>
/// <para><strong>POST vs PUT vs DELETE</strong></para>
/// <list type="bullet">
///   <item><description><c>DELETE</c> wipes included collections (paginated internally).</description></item>
///   <item><description><c>POST</c> imports only into <em>empty</em> included collections (HTTP 409 if not empty — use DELETE or PUT).</description></item>
///   <item><description><c>PUT</c> merges into existing data (<c>strategy=skip</c> or <c>replace</c>).</description></item>
/// </list>
/// <para>
/// Endpoints are gated by <see cref="SeedEndpointGateFilter"/> so production clusters can disable
/// destructive seed APIs when they are not needed.
/// </para>
/// </remarks>
[ApiController]
[Route("api/v2/seed")]
[Tags("Seeding")]
[ServiceFilter(typeof(SeedEndpointGateFilter))]
public class SeedController(
    ISeedCatalogService seedCatalogService,
    SeedOperationGate seedOperationGate) : ControllerBase
{
    /// <summary>
    /// Exports seed data from the running instance.
    /// </summary>
    /// <param name="include">
    /// Comma-separated collections to export. Defaults to all catalog collections when omitted.
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the export before it completes.</param>
    /// <returns>JSON seed bundle.</returns>
    /// <response code="200">Returns the exported seed data.</response>
    /// <response code="400">The <paramref name="include"/> parameter is invalid.</response>
    /// <response code="409">Another seed operation is already running.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpGet]
    [ProducesResponseType(typeof(SeedOptions), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Export([FromQuery] string? include, CancellationToken cancellationToken)
    {
        var collections = ParseInclude(include);
        var seed = await seedOperationGate.RunAsync(
            token => seedCatalogService.ExportAsync(collections, token),
            cancellationToken);
        return Ok(seed);
    }

    /// <summary>
    /// Wipes included seed collections so a subsequent POST can import into an empty target.
    /// </summary>
    /// <param name="include">Comma-separated collections to delete.</param>
    /// <param name="cancellationToken">Token used to cancel the delete before it completes.</param>
    /// <returns>Summary of deleted records per collection.</returns>
    /// <response code="200">The delete completed successfully.</response>
    /// <response code="400">The <paramref name="include"/> parameter is invalid.</response>
    /// <response code="409">Another seed operation is already running.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpDelete]
    [ProducesResponseType(typeof(SeedImportSummary), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Delete([FromQuery] string? include, CancellationToken cancellationToken)
    {
        var collections = ParseInclude(include);
        var summary = await seedOperationGate.RunAsync(
            token => seedCatalogService.DeleteAsync(collections, token),
            cancellationToken);
        return Ok(summary);
    }

    /// <summary>
    /// Imports seed data into empty included collections.
    /// </summary>
    /// <param name="include">Comma-separated collections to import.</param>
    /// <param name="cancellationToken">Token used to cancel the import before it completes.</param>
    /// <returns>Summary of imported records per collection.</returns>
    /// <remarks>
    /// Returns HTTP 409 when any included collection already has data.
    /// Use <see cref="Delete"/> first to wipe, or <see cref="Import"/> with PUT to merge.
    /// </remarks>
    /// <response code="200">The import completed successfully.</response>
    /// <response code="400">The request body is missing or <paramref name="include"/> is invalid.</response>
    /// <response code="409">Target collections are not empty or another seed operation is running.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(SeedImportSummary), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> ReplaceWholesale([FromQuery] string? include, CancellationToken cancellationToken)
    {
        var collections = ParseInclude(include);
        var seed = await ReadJsonBodyAsync<SeedOptions>(cancellationToken)
            ?? throw new BadRequestException("Request body is required.");
        var summary = await seedOperationGate.RunAsync(
            token => seedCatalogService.ImportPostAsync(seed, collections, token),
            cancellationToken);
        return Ok(summary);
    }

    /// <summary>
    /// Merges seed data using a per-ID strategy.
    /// </summary>
    /// <param name="include">Comma-separated collections to import.</param>
    /// <param name="strategy"><c>skip</c> creates missing IDs only; <c>replace</c> upserts by ID.</param>
    /// <param name="cancellationToken">Token used to cancel the import before it completes.</param>
    /// <returns>Summary of imported or updated records per collection.</returns>
    /// <response code="200">The merge completed successfully.</response>
    /// <response code="400">The request body is missing, <paramref name="strategy"/> is invalid, or <paramref name="include"/> is invalid.</response>
    /// <response code="409">Another seed operation is already running.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpPut]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(SeedImportSummary), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> Import(
        [FromQuery] string? include,
        [FromQuery] string strategy = "skip",
        CancellationToken cancellationToken = default)
    {
        var collections = ParseInclude(include);
        var importStrategy = ParseStrategy(strategy);
        var seed = await ReadJsonBodyAsync<SeedOptions>(cancellationToken)
            ?? throw new BadRequestException("Request body is required.");
        var summary = await seedOperationGate.RunAsync(
            token => seedCatalogService.ImportWithStrategyAsync(seed, collections, importStrategy, token),
            cancellationToken);
        return Ok(summary);
    }

    private async Task<T?> ReadJsonBodyAsync<T>(CancellationToken cancellationToken) where T : class
    {
        if (Request.ContentLength is 0)
        {
            return null;
        }

        return await System.Text.Json.JsonSerializer.DeserializeAsync<T>(Request.Body, cancellationToken: cancellationToken);
    }

    private static SeedCollections ParseInclude(string? include)
    {
        try
        {
            return SeedCollectionParser.Parse(include);
        }
        catch (ArgumentException exception)
        {
            throw new BadRequestException(exception.Message);
        }
    }

    private static SeedImportStrategy ParseStrategy(string strategy) =>
        strategy.ToLowerInvariant() switch
        {
            "skip" => SeedImportStrategy.Skip,
            "replace" => SeedImportStrategy.Replace,
            _ => throw new BadRequestException("Strategy must be 'skip' or 'replace'.")
        };
}
