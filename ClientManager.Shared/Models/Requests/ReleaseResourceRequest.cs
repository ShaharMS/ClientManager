namespace ClientManager.Shared.Models.Requests;

/// <summary>
/// Query parameters for releasing a previously acquired resource.
/// </summary>
/// <param name="AllocationId">The ID of the resource allocation to release.</param>
public record ReleaseResourceRequest(string AllocationId);
