namespace ClientManager.Api.Models.Responses;

/// <summary>
/// Summary of all clients with their service and pool access statistics for the dashboard.
/// </summary>
/// <param name="Rows">List of per-client summary rows.</param>
public record ClientSummariesResponse(
    List<ClientSummaryRow> Rows
);

/// <summary>
/// A single row in the client summary table.
/// </summary>
/// <param name="ClientId">The unique identifier of the client.</param>
/// <param name="DisplayName">Human-readable display name.</param>
/// <param name="AccessibleServices">Number of services the client can access.</param>
/// <param name="TotalRateLimitCap">Formatted string summarizing the client's total rate limit capacity.</param>
/// <param name="AccessiblePools">Number of resource pools the client has access to.</param>
/// <param name="UsedSlots">Number of resource pool slots currently in use.</param>
/// <param name="TotalAccessibleSlots">Total number of resource pool slots the client can access.</param>
public record ClientSummaryRow(
    string ClientId,
    string DisplayName,
    int AccessibleServices,
    string TotalRateLimitCap,
    int AccessiblePools,
    int UsedSlots,
    int TotalAccessibleSlots
);
