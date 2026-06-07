using Asp.Versioning;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Shared.Contracts.Statistics;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Problems;
using ClientManager.Shared.Models.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.Api.Controllers;

/// <summary>
/// Usage time-series and historical statistics endpoints.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/statistics")]
[Tags("Statistics")]
public class StatisticsUsageController(IStatisticsService statisticsService) : ControllerBase
{
    /// <summary>
    /// Retrieves usage over time for one or more services or resource pools.
    /// </summary>
    /// <param name="filterType">The target type: Service or ResourcePool.</param>
    /// <param name="targetIds">Comma-separated IDs of the services or resource pools.</param>
    /// <param name="clientIds">Optional comma-separated client IDs to filter by.</param>
    /// <param name="from">Optional start of the time range (UTC, ISO 8601).</param>
    /// <param name="to">Optional end of the time range (UTC, ISO 8601).</param>
    /// <param name="granularity">Optional bucket granularity: Second, FiveMinute, Hour, or Day.</param>
    /// <param name="cancellationToken">Token used to cancel the usage time-series aggregation before it completes.</param>
    /// <returns>Per-target time-series data for usage and capacity.</returns>
    /// <response code="200">Returns per-target usage time-series data.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpGet("usage-timeseries")]
    [ProducesResponseType(typeof(List<TargetUsageTimeSeriesResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetUsageTimeSeries(
        [FromQuery] TargetType filterType,
        [FromQuery] IdentifierList targetIds,
        [FromQuery] IdentifierList? clientIds,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] BucketGranularity? granularity,
        CancellationToken cancellationToken)
    {
        var usageTimeSeries = await statisticsService.GetUsageTimeSeriesAsync(
            filterType,
            targetIds,
            clientIds,
            from,
            to,
            granularity,
            cancellationToken);
        return Ok(usageTimeSeries);
    }

    /// <summary>
    /// Retrieves per-client usage breakdown for one or more services or resource pools.
    /// </summary>
    /// <param name="filterType">The target type: Service or ResourcePool.</param>
    /// <param name="targetIds">Comma-separated IDs of the services or resource pools.</param>
    /// <param name="clientIds">Optional comma-separated client IDs to filter by.</param>
    /// <param name="from">Optional start of the time range (UTC, ISO 8601).</param>
    /// <param name="to">Optional end of the time range (UTC, ISO 8601).</param>
    /// <param name="granularity">Optional bucket granularity: Second, FiveMinute, Hour, or Day.</param>
    /// <param name="cancellationToken">Token used to cancel the client usage breakdown aggregation before it completes.</param>
    /// <returns>Per-target client usage breakdowns.</returns>
    /// <response code="200">Returns per-target client usage breakdowns.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpGet("client-usage-breakdown")]
    [ProducesResponseType(typeof(List<TargetClientUsageBreakdownResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetClientUsageBreakdown(
        [FromQuery] TargetType filterType,
        [FromQuery] IdentifierList targetIds,
        [FromQuery] IdentifierList? clientIds,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] BucketGranularity? granularity,
        CancellationToken cancellationToken)
    {
        var clientUsageBreakdown = await statisticsService.GetClientUsageBreakdownAsync(
            filterType,
            targetIds,
            clientIds,
            from,
            to,
            granularity,
            cancellationToken);
        return Ok(clientUsageBreakdown);
    }

    /// <summary>
    /// Retrieves historical usage data for one or more services or resource pools over a time range.
    /// </summary>
    /// <param name="filterType">The target type: Service or ResourcePool.</param>
    /// <param name="targetIds">Comma-separated IDs of the services or resource pools.</param>
    /// <param name="clientId">Optional: filter to a single client.</param>
    /// <param name="from">Start of the time range (UTC, ISO 8601).</param>
    /// <param name="to">End of the time range (UTC, ISO 8601).</param>
    /// <param name="granularity">Bucket granularity: Second, FiveMinute, Hour, or Day.</param>
    /// <param name="cancellationToken">Token used to cancel the historical usage query before it completes.</param>
    /// <returns>Historical usage data points per target within the requested range.</returns>
    /// <response code="200">Returns the historical usage data.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpGet("historical-usage")]
    [ProducesResponseType(typeof(List<HistoricalUsageResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetHistoricalUsage(
        [FromQuery] TargetType filterType,
        [FromQuery] IdentifierList targetIds,
        [FromQuery] string? clientId,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] BucketGranularity granularity,
        CancellationToken cancellationToken)
    {
        var historicalUsage = await statisticsService.GetHistoricalUsageAsync(
            filterType, targetIds, clientId, from, to, granularity, cancellationToken);

        return Ok(historicalUsage);
    }

    /// <summary>
    /// Retrieves historical usage data for one or more services or resource pools split by client.
    /// </summary>
    /// <param name="filterType">The target type: Service or ResourcePool.</param>
    /// <param name="targetIds">Comma-separated IDs of the services or resource pools.</param>
    /// <param name="clientIds">Comma-separated client IDs included in the response.</param>
    /// <param name="from">Start of the time range (UTC, ISO 8601).</param>
    /// <param name="to">End of the time range (UTC, ISO 8601).</param>
    /// <param name="granularity">Bucket granularity: Second, FiveMinute, Hour, or Day.</param>
    /// <param name="cancellationToken">Token used to cancel the per-client historical usage query before it completes.</param>
    /// <returns>Historical usage data points for each requested target-client pair.</returns>
    /// <response code="200">Returns the historical usage data for each requested target-client pair.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpGet("historical-usage/by-client")]
    [ProducesResponseType(typeof(List<ClientHistoricalUsageResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetHistoricalUsageByClient(
        [FromQuery] TargetType filterType,
        [FromQuery] IdentifierList targetIds,
        [FromQuery] IdentifierList clientIds,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] BucketGranularity granularity,
        CancellationToken cancellationToken)
    {
        var clientHistoricalUsage = await statisticsService.GetHistoricalUsageByClientAsync(
            filterType,
            targetIds,
            clientIds,
            from,
            to,
            granularity,
            cancellationToken);

        return Ok(clientHistoricalUsage);
    }
}
