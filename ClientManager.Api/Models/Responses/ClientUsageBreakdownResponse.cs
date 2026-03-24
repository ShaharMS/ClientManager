namespace ClientManager.Api.Models.Responses;

/// <summary>
/// Per-client usage breakdown for a specific service or resource pool.
/// </summary>
/// <param name="Entries">List of per-client usage entries.</param>
public record ClientUsageBreakdownResponse(
    IReadOnlyList<ClientUsageEntry> Entries
);

/// <summary>
/// A single client's usage value within a breakdown.
/// </summary>
/// <param name="ClientId">The unique identifier of the client.</param>
/// <param name="ClientName">Human-readable display name of the client.</param>
/// <param name="Value">The usage value for this client.</param>
/// <param name="GrantedCount">Total granted requests across the requested window.</param>
/// <param name="DeniedCount">Total denied requests across the requested window.</param>
/// <param name="ActiveCount">Latest active allocation count within the requested window.</param>
public record ClientUsageEntry(
    string ClientId,
    string ClientName,
    double Value,
    long GrantedCount,
    long DeniedCount,
    long ActiveCount
);
