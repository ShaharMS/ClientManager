namespace ClientManager.Api.Models.Exceptions;

/// <summary>
/// Thrown when a client has no resource pool settings for the requested pool.
/// </summary>
public class ResourcePoolSettingsNotFoundException(string resourcePoolId, string clientId) : NotFoundException($"Resource pool settings for '{resourcePoolId}' not found on client '{clientId}'")
{
    public string ResourcePoolId { get; } = resourcePoolId;
    public string ClientId { get; } = clientId;
}
