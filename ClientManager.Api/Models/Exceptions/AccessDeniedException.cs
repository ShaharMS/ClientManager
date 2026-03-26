namespace ClientManager.Api.Models.Exceptions;

/// <summary>
/// Thrown when a client has a configuration entry for a service but access is explicitly
/// denied (<c>IsAllowed = false</c>). Mapped to HTTP 403 by the error-handling middleware.
/// </summary>
public class AccessDeniedException : Exception
{
    public string ClientId { get; }
    public string ServiceId { get; }

    public AccessDeniedException(string clientId, string serviceId)
        : base($"Client '{clientId}' does not have access to service '{serviceId}'")
    {
        ClientId = clientId;
        ServiceId = serviceId;
    }
}
