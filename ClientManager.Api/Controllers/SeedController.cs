using Asp.Versioning;
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
/// Exports and imports catalog and statistics seed data for copying configuration and usage history between instances.
/// </summary>
/// <remarks>
/// <para><strong>Response formats</strong></para>
/// <list type="bullet">
///   <item><description>Catalog-only requests return JSON in the <see cref="SeedOptions"/> shape (paste into appsettings).</description></item>
///   <item><description>Requests that include <c>usageSnapshots</c>, or set <c>format=ndjson</c>, stream <c>seed.ndjson</c> (one entity per line).</description></item>
/// </list>
/// <para><strong>Long-running operations</strong></para>
/// <para>
/// Export, import, and delete can take minutes to hours when statistics volumes are large.
/// Only one seed operation may run at a time (concurrent requests receive HTTP 409).
/// Do not start another seed call until the current one finishes.
/// </para>
/// <para><strong>POST vs PUT vs DELETE</strong></para>
/// <list type="bullet">
///   <item><description><c>DELETE</c> wipes included collections (paginated internally).</description></item>
///   <item><description><c>POST</c> imports only into <em>empty</em> included collections (HTTP 409 if not empty — use DELETE or PUT).</description></item>
///   <item><description><c>PUT</c> merges into existing data (<c>strategy=skip</c> or <c>replace</c>).</description></item>
/// </list>
/// </remarks>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/seed")]
[Tags("Seeding")]
public class SeedController(
    ISeedCatalogService seedCatalogService,
    SeedOperationGate seedOperationGate) : ControllerBase
{
    /// <summary>
    /// Exports seed data from the running instance.
    /// </summary>
    /// <param name="include">
    /// Comma-separated collections to export. Defaults to all catalog collections when omitted.
    /// Include <c>usageSnapshots</c> to export statistics (forces NDJSON download).
    /// </param>
    /// <param name="format">
    /// Set to <c>ndjson</c> to download <c>seed.ndjson</c> even for catalog-only exports.
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the export before it completes.</param>
    /// <returns>JSON seed bundle or streamed <c>seed.ndjson</c> file.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(SeedOptions), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Export(
        [FromQuery] string? include,
        [FromQuery] string? format,
        CancellationToken cancellationToken)
    {
        var collections = ParseInclude(include);

        if (SeedCollectionParser.UsesNdjson(collections, format))
        {
            return await seedOperationGate.RunAsync(async token =>
            {
                Response.ContentType = "application/x-ndjson";
                Response.Headers.ContentDisposition = "attachment; filename=\"seed.ndjson\"";
                await seedCatalogService.ExportNdjsonAsync(collections, Response.Body, token);
                return new EmptyResult();
            }, cancellationToken);
        }

        var seed = await seedOperationGate.RunAsync(
            token => seedCatalogService.ExportAsync(collections, token),
            cancellationToken);
        return Ok(seed);
    }

    /// <summary>
    /// Wipes included seed collections so a subsequent POST can import into an empty target.
    /// </summary>
    /// <param name="include">Comma-separated collections to delete.</param>
    /// <param name="format">Set to <c>ndjson</c> for an NDJSON progress stream on catalog-only deletes.</param>
    /// <param name="cancellationToken">Token used to cancel the delete before it completes.</param>
    /// <returns>NDJSON progress stream ending with a <c>_summary</c> line when statistics are included; JSON summary for catalog-only.</returns>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SeedImportSummary), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Delete(
        [FromQuery] string? include,
        [FromQuery] string? format,
        CancellationToken cancellationToken)
    {
        var collections = ParseInclude(include);

        if (SeedCollectionParser.UsesNdjson(collections, format))
        {
            return await seedOperationGate.RunAsync(async token =>
            {
                Response.ContentType = "application/x-ndjson";
                await seedCatalogService.DeleteAsync(collections, Response.Body, token);
                return new EmptyResult();
            }, cancellationToken);
        }

        var summary = await seedOperationGate.RunAsync(
            token => seedCatalogService.DeleteAsync(collections, progressOutput: null, token),
            cancellationToken);
        return Ok(summary);
    }

    /// <summary>
    /// Imports seed data into empty included collections.
    /// </summary>
    /// <remarks>
    /// Returns HTTP 409 when any included collection already has data.
    /// Use <see cref="Delete"/> first to wipe, or <see cref="Import"/> with PUT to merge.
    /// Accepts <c>application/json</c> (catalog) or <c>application/x-ndjson</c> (migration file).
    /// NDJSON responses stream <c>_progress</c> and <c>_summary</c> lines.
    /// </remarks>
    [HttpPost]
    [Consumes("application/json", "application/x-ndjson")]
    [ProducesResponseType(typeof(SeedImportSummary), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> ReplaceWholesale(
        [FromQuery] string? include,
        CancellationToken cancellationToken)
    {
        var collections = ParseInclude(include);

        if (IsNdjsonRequest())
        {
            return await seedOperationGate.RunAsync(async token =>
            {
                Response.ContentType = "application/x-ndjson";
                await seedCatalogService.ImportPostNdjsonAsync(Request.Body, collections, Response.Body, token);
                return new EmptyResult();
            }, cancellationToken);
        }

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
    [HttpPut]
    [Consumes("application/json", "application/x-ndjson")]
    [ProducesResponseType(typeof(SeedImportSummary), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
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

        if (IsNdjsonRequest())
        {
            return await seedOperationGate.RunAsync(async token =>
            {
                Response.ContentType = "application/x-ndjson";
                await seedCatalogService.ImportNdjsonAsync(
                    Request.Body,
                    collections,
                    importStrategy,
                    Response.Body,
                    token);
                return new EmptyResult();
            }, cancellationToken);
        }

        var seed = await ReadJsonBodyAsync<SeedOptions>(cancellationToken)
            ?? throw new BadRequestException("Request body is required.");

        var summary = await seedOperationGate.RunAsync(
            token => seedCatalogService.ImportWithStrategyAsync(seed, collections, importStrategy, token),
            cancellationToken);
        return Ok(summary);
    }

    private bool IsNdjsonRequest() =>
        Request.ContentType is not null &&
        Request.ContentType.Contains("application/x-ndjson", StringComparison.OrdinalIgnoreCase);

    private async Task<T?> ReadJsonBodyAsync<T>(CancellationToken cancellationToken) where T : class
    {
        if (Request.ContentLength is 0)
        {
            return null;
        }

        return await System.Text.Json.JsonSerializer.DeserializeAsync<T>(
            Request.Body,
            cancellationToken: cancellationToken);
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
