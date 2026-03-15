namespace ClientManager.Api.Models.Requests;

/// <summary>
/// Request model for acquiring a resource from a resource pool.
/// </summary>
/// <param name="ClientId">The ID of the client requesting the resource.</param>
/// <param name="ResourcePoolId">The ID of the resource pool to acquire from.</param>
public record AcquireResourceRequest(string ClientId, string ResourcePoolId);
