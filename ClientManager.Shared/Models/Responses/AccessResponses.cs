namespace ClientManager.Shared.Models.Responses;

/// <summary>
/// Response returned when a client is granted access to a service.
/// </summary>
/// <remarks>
/// Returned by <c>GET /api/v2/access/check</c> with HTTP 200. Gateways may forward
/// <see cref="RemainingRequests"/> to callers as a hint about remaining per-client capacity.
/// </remarks>
public record AccessCheckResponse
{
    /// <summary>
    /// The unique identifier of the client that was checked.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// The unique identifier of the service that was checked.
    /// </summary>
    public required string ServiceId { get; init; }

    /// <summary>
    /// Remaining per-client request capacity after this check, when a per-client limit applies.
    /// May be <c>null</c> when no applicable limit exposes a remaining count.
    /// </summary>
    public int? RemainingRequests { get; init; }
}
