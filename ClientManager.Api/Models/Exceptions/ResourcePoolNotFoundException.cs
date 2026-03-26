namespace ClientManager.Api.Models.Exceptions;

/// <summary>
/// Thrown when a resource pool definition cannot be found by its identifier.
/// </summary>
public class ResourcePoolNotFoundException(string resourcePoolId) : NotFoundException($"Resource pool '{resourcePoolId}' not found")
{
    public string ResourcePoolId { get; } = resourcePoolId;
}
