namespace ClientManager.Shared.Models.Requests;

/// <summary>
/// Query parameters for checking if a client has access to a service.
/// </summary>
/// <param name="ClientId">The ID of the client.</param>
/// <param name="ServiceId">The ID of the service.</param>
public record CheckAccessRequest(string ClientId, string ServiceId);
