namespace ClientManager.Shared.Models.Requests;

/// <summary>
/// Query parameters for checking if a client has access to a service.
/// </summary>
/// <remarks>
/// Bound from <c>GET /api/v2/access/check</c>. Both identifiers are required.
/// </remarks>
/// <param name="ClientId">The unique identifier of the client.</param>
/// <param name="ServiceId">The unique identifier of the service.</param>
public record CheckAccessRequest(string ClientId, string ServiceId);
