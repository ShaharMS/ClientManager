namespace ClientManager.Api.Models.Exceptions;

/// <summary>
/// Thrown when a client has no service access settings for the requested service.
/// </summary>
public class ServiceSettingsNotFoundException(string serviceId, string clientId) : NotFoundException($"Service settings for '{serviceId}' not found on client '{clientId}'")
{
    public string ServiceId { get; } = serviceId;
    public string ClientId { get; } = clientId;
}
