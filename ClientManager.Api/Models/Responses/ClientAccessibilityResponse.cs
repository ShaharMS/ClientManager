namespace ClientManager.Api.Models.Responses;

public record ClientAccessibilityResponse
{
    public required string ClientId { get; init; }
    public IReadOnlyList<ServiceAccessibility> Services { get; init; } = [];
}

public record ServiceAccessibility
{
    public required string ServiceId { get; init; }
    public bool HasAccess { get; init; }
    public bool IsCurrentlyRateLimited { get; init; }
    public int? RemainingRequests { get; init; }
}
