namespace ClientManager.Api.Models.Exceptions;

/// <summary>
/// Thrown when a resource allocation cannot be found by its identifier.
/// </summary>
public class AllocationNotFoundException(string allocationId) : NotFoundException($"Allocation '{allocationId}' not found")
{
    public string AllocationId { get; } = allocationId;
}
