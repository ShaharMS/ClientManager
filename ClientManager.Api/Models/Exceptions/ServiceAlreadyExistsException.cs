namespace ClientManager.Api.Models.Exceptions;

/// <summary>
/// Thrown when a service with the same identifier already exists.
/// </summary>
public sealed class ServiceAlreadyExistsException(string serviceId)
    : ConflictException($"Service '{serviceId}' already exists")
{
    public string ServiceId { get; } = serviceId;
}