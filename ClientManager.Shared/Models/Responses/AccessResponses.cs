namespace ClientManager.Shared.Models.Responses;

/// <summary>
/// Response model for checking if a client has access to a service.
/// </summary>
public record AccessCheckResponse
{
    /// <summary>
    /// The ID of the client.
    /// </summary>
    public required string ClientId { get; init; }
    /// <summary>
    /// The ID of the service.
    /// </summary>
    public required string ServiceId { get; init; }
    /// <summary>
    /// The number of remaining requests the client can make.
    /// </summary>
    public int? RemainingRequests { get; init; }
}
