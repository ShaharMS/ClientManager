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
[ApiController]
[Route("api/v1/seed")]
[Tags("Seeding")]
[ServiceFilter(typeof(SeedEndpointGateFilter))]
public class SeedController(
    ISeedCatalogService seedCatalogService,
    SeedOperationGate seedOperationGate) : ControllerBase
{
    /// <summary>
    /// Exports seed data from the running instance.
    /// </summary>
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
