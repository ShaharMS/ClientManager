using Asp.Versioning;
using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Utils;
using ClientManager.Shared.Configuration.Storage;
using ClientManager.Shared.Models.Problems;
using ClientManager.Shared.Models.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.Api.Controllers;

/// <summary>
/// Exports and imports catalog seed data for copying permissions between instances.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/seed")]
[Tags("Seeding")]
public class SeedController(ISeedCatalogService seedCatalogService) : ControllerBase
{
    /// <summary>
    /// Exports catalog seed data from the running instance.
    /// </summary>
    /// <param name="include">
    /// Comma-separated collections to export. Defaults to all catalog collections when omitted.
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the export before it completes.</param>
    /// <returns>Seed bundle compatible with appsettings <c>Seed</c> and import endpoints.</returns>
    /// <response code="200">Returns the exported seed bundle.</response>
    /// <response code="400">Invalid <paramref name="include"/> value.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpGet]
    [ProducesResponseType(typeof(SeedOptions), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Export([FromQuery] string? include, CancellationToken cancellationToken)
    {
        var collections = ParseInclude(include);
        var seed = await seedCatalogService.ExportAsync(collections, cancellationToken);
        return Ok(seed);
    }

    /// <summary>
    /// Replaces selected catalog collections wholesale with the request body.
    /// </summary>
    /// <param name="seed">Catalog entities to import.</param>
    /// <param name="include">
    /// Comma-separated collections to replace. Defaults to all catalog collections when omitted.
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the import before it completes.</param>
    /// <returns>Counts of deleted and created entities.</returns>
    /// <response code="200">The wholesale replace completed.</response>
    /// <response code="400">Invalid <paramref name="include"/> value or missing/invalid request body.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpPost]
    [ProducesResponseType(typeof(SeedImportSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ReplaceWholesale(
        [FromBody] SeedOptions seed,
        [FromQuery] string? include,
        CancellationToken cancellationToken)
    {
        var collections = ParseInclude(include);
        var summary = await seedCatalogService.ReplaceWholesaleAsync(seed, collections, cancellationToken);
        return Ok(summary);
    }

    /// <summary>
    /// Imports catalog entities using a per-ID strategy.
    /// </summary>
    /// <param name="seed">Catalog entities to import.</param>
    /// <param name="include">
    /// Comma-separated collections to import. Defaults to all catalog collections when omitted.
    /// </param>
    /// <param name="strategy">
    /// <c>skip</c> creates missing IDs only; <c>replace</c> upserts by ID without deleting unmentioned entities.
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the import before it completes.</param>
    /// <returns>Counts of created, updated, and skipped entities.</returns>
    /// <response code="200">The import completed.</response>
    /// <response code="400">Invalid <paramref name="include"/>, <paramref name="strategy"/>, or request body.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpPut]
    [ProducesResponseType(typeof(SeedImportSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Import(
        [FromBody] SeedOptions seed,
        [FromQuery] string? include,
        [FromQuery] string strategy = "skip",
        CancellationToken cancellationToken = default)
    {
        var collections = ParseInclude(include);
        var importStrategy = ParseStrategy(strategy);
        var summary = await seedCatalogService.ImportWithStrategyAsync(seed, collections, importStrategy, cancellationToken);
        return Ok(summary);
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
