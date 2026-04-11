namespace ClientManager.Api.Models.Exceptions;

/// <summary>
/// Thrown when a resource pool with the same identifier already exists.
/// </summary>
public sealed class ResourcePoolAlreadyExistsException(string resourcePoolId)
    : ConflictException($"Resource pool '{resourcePoolId}' already exists")
{
    public string ResourcePoolId { get; } = resourcePoolId;
}