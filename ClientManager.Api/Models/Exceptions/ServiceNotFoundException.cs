namespace ClientManager.Api.Models.Exceptions;

/// <summary>
/// Thrown when a service definition cannot be found by its identifier.
/// </summary>
public class ServiceNotFoundException(string serviceId) : NotFoundException($"Service '{serviceId}' not found")
{
    public string ServiceId { get; } = serviceId;
}
